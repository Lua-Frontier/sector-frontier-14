using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Reflection.Emit;

namespace InspectInlineArray;

internal readonly record struct TypeMark(string Display, bool HasInlineArray);

internal sealed class TypeMarkProvider : ISignatureTypeProvider<TypeMark, object?>
{
    private readonly MetadataReader _reader;

    public TypeMarkProvider(MetadataReader reader)
    {
        _reader = reader;
    }

    public TypeMark GetArrayType(TypeMark elementType, ArrayShape shape) =>
        new($"{elementType.Display}[{new string(',', shape.Rank - 1)}]", elementType.HasInlineArray);

    public TypeMark GetByReferenceType(TypeMark elementType) =>
        new($"{elementType.Display}&", elementType.HasInlineArray);

    public TypeMark GetFunctionPointerType(MethodSignature<TypeMark> signature) =>
        new("fnptr", signature.ReturnType.HasInlineArray || signature.ParameterTypes.Any(p => p.HasInlineArray));

    public TypeMark GetGenericInstantiation(TypeMark genericType, ImmutableArray<TypeMark> typeArguments) =>
        new($"{genericType.Display}<{string.Join(", ", typeArguments.Select(a => a.Display))}>",
            genericType.HasInlineArray || typeArguments.Any(a => a.HasInlineArray));

    public TypeMark GetGenericMethodParameter(object? genericContext, int index) => new($"!!{index}", false);

    public TypeMark GetGenericTypeParameter(object? genericContext, int index) => new($"!{index}", false);

    public TypeMark GetModifiedType(TypeMark modifier, TypeMark unmodifiedType, bool isRequired) =>
        new(unmodifiedType.Display, modifier.HasInlineArray || unmodifiedType.HasInlineArray);

    public TypeMark GetPinnedType(TypeMark elementType) => new($"{elementType.Display} pinned", elementType.HasInlineArray);

    public TypeMark GetPointerType(TypeMark elementType) => new($"{elementType.Display}*", elementType.HasInlineArray);

    public TypeMark GetPrimitiveType(PrimitiveTypeCode typeCode) => new(typeCode.ToString(), false);

    public TypeMark GetSZArrayType(TypeMark elementType) => new($"{elementType.Display}[]", elementType.HasInlineArray);

    public TypeMark GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
    {
        var def = reader.GetTypeDefinition(handle);
        var ns = reader.GetString(def.Namespace);
        var name = reader.GetString(def.Name);
        var full = string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
        var inline = name.StartsWith("<>y__InlineArray", StringComparison.Ordinal);
        return new(full, inline);
    }

    public TypeMark GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
    {
        var tr = reader.GetTypeReference(handle);
        var ns = reader.GetString(tr.Namespace);
        var name = reader.GetString(tr.Name);
        var full = string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
        return new(full, false);
    }

    public TypeMark GetTypeFromSpecification(MetadataReader reader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
    {
        var ts = reader.GetTypeSpecification(handle);
        var decoder = new SignatureDecoder<TypeMark, object?>(this, reader, genericContext);
        var br = reader.GetBlobReader(ts.Signature);
        return decoder.DecodeType(ref br);
    }
}

internal static class Program
{
    private static readonly Dictionary<ushort, OpCode> OpCodeMap = BuildOpCodeMap();

    private static Dictionary<ushort, OpCode> BuildOpCodeMap()
    {
        var dict = new Dictionary<ushort, OpCode>();
        foreach (var f in typeof(OpCodes).GetFields().Where(f => f.FieldType == typeof(OpCode)))
        {
            var op = (OpCode) f.GetValue(null)!;
            dict[(ushort) op.Value] = op;
        }

        return dict;
    }

    private static int OperandSize(byte[] il, int operandStart, OperandType operandType)
    {
        return operandType switch
        {
            OperandType.InlineNone => 0,
            OperandType.ShortInlineBrTarget => 1,
            OperandType.ShortInlineI => 1,
            OperandType.ShortInlineVar => 1,
            OperandType.InlineVar => 2,
            OperandType.InlineI => 4,
            OperandType.InlineBrTarget => 4,
            OperandType.InlineField => 4,
            OperandType.InlineMethod => 4,
            OperandType.InlineSig => 4,
            OperandType.InlineString => 4,
            OperandType.InlineTok => 4,
            OperandType.InlineType => 4,
            OperandType.ShortInlineR => 4,
            OperandType.InlineI8 => 8,
            OperandType.InlineR => 8,
            OperandType.InlineSwitch => 4 + BitConverter.ToInt32(il, operandStart) * 4,
            _ => 0
        };
    }

    private static string FormatMethodRef(MetadataReader reader, EntityHandle handle)
    {
        try
        {
            if (handle.Kind == HandleKind.MemberReference)
            {
                var mr = reader.GetMemberReference((MemberReferenceHandle) handle);
                return $"{GetFullTypeName(reader, mr.Parent)}.{reader.GetString(mr.Name)}";
            }

            if (handle.Kind == HandleKind.MethodDefinition)
            {
                var md = reader.GetMethodDefinition((MethodDefinitionHandle) handle);
                return $"{GetFullTypeName(reader, md.GetDeclaringType())}.{reader.GetString(md.Name)}";
            }
        }
        catch
        {
            // ignored
        }

        return $"<{handle.Kind}>";
    }

    private static IEnumerable<string> GuessInlineArrayConsumers(PEReader pe, MetadataReader reader, string typeName, string methodName)
    {
        // Find method handle
        MethodDefinitionHandle? targetHandle = null;

        foreach (var tdHandle in reader.TypeDefinitions)
        {
            if (GetFullTypeNameDef(reader, tdHandle) != typeName)
                continue;

            var td = reader.GetTypeDefinition(tdHandle);
            foreach (var mdHandle in td.GetMethods())
            {
                var md = reader.GetMethodDefinition(mdHandle);
                if (reader.GetString(md.Name) == methodName)
                {
                    targetHandle = mdHandle;
                    break;
                }
            }
        }

        if (targetHandle == null)
            yield break;

        var target = reader.GetMethodDefinition(targetHandle.Value);
        if (target.RelativeVirtualAddress == 0)
            yield break;

        var typeProvider = new TypeMarkProvider(reader);

        // Find type handles for <>y__InlineArray* (generic type definitions)
        var inlineArrayTypeDefs = new HashSet<TypeDefinitionHandle>();
        foreach (var tdHandle in reader.TypeDefinitions)
        {
            var td = reader.GetTypeDefinition(tdHandle);
            var name = reader.GetString(td.Name);
            if (name.StartsWith("<>y__InlineArray", StringComparison.Ordinal))
                inlineArrayTypeDefs.Add(tdHandle);
        }

        var body = pe.GetMethodBody(target.RelativeVirtualAddress);
        var il = body.GetILBytes();

        var pendingCalls = 0;
        var consumers = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < il.Length;)
        {
            ushort opVal;
            int opSize;

            if (il[i] == 0xFE && i + 1 < il.Length)
            {
                opVal = (ushort) (0xFE00 | il[i + 1]);
                opSize = 2;
            }
            else
            {
                opVal = il[i];
                opSize = 1;
            }

            if (!OpCodeMap.TryGetValue(opVal, out var op))
            {
                i += opSize;
                continue;
            }

            var operandStart = i + opSize;
            var operandLen = OperandSize(il, operandStart, op.OperandType);

            // If instruction references a type token, see if it's our inline array (direct or via TypeSpec)
            if (op.OperandType is OperandType.InlineType or OperandType.InlineTok)
            {
                if (operandStart + 4 <= il.Length)
                {
                    var token = BitConverter.ToInt32(il, operandStart);
                    var h = MetadataTokens.EntityHandle(token);
                    if (h.Kind == HandleKind.TypeDefinition && inlineArrayTypeDefs.Contains((TypeDefinitionHandle) h))
                    {
                        pendingCalls = 8;
                    }
                    else if (h.Kind == HandleKind.TypeSpecification)
                    {
                        var ts = reader.GetTypeSpecification((TypeSpecificationHandle) h);
                        var br = reader.GetBlobReader(ts.Signature);
                        var dec = new SignatureDecoder<TypeMark, object?>(typeProvider, reader, genericContext: null);
                        var decoded = dec.DecodeType(ref br);
                        if (decoded.HasInlineArray)
                            pendingCalls = 8;
                    }
                }
            }

            // initobj uses InlineType but shows up as OpCode 0xFE15 (we already handle)
            // stfld/ldfld can reference inline array element fields too, via InlineField.
            if (op.OperandType == OperandType.InlineField && operandStart + 4 <= il.Length)
            {
                var token = BitConverter.ToInt32(il, operandStart);
                var h = MetadataTokens.EntityHandle(token);
                if (h.Kind == HandleKind.FieldDefinition)
                {
                    var fd = reader.GetFieldDefinition((FieldDefinitionHandle) h);
                    var parent = fd.GetDeclaringType();
                    if (inlineArrayTypeDefs.Contains(parent))
                        pendingCalls = 8;
                }
                else if (h.Kind == HandleKind.MemberReference)
                {
                    var mr = reader.GetMemberReference((MemberReferenceHandle) h);
                    if (mr.Parent.Kind == HandleKind.TypeDefinition && inlineArrayTypeDefs.Contains((TypeDefinitionHandle) mr.Parent))
                    {
                        pendingCalls = 8;
                    }
                    else if (mr.Parent.Kind == HandleKind.TypeSpecification)
                    {
                        var ts = reader.GetTypeSpecification((TypeSpecificationHandle) mr.Parent);
                        var br = reader.GetBlobReader(ts.Signature);
                        var dec = new SignatureDecoder<TypeMark, object?>(typeProvider, reader, genericContext: null);
                        var decoded = dec.DecodeType(ref br);
                        if (decoded.HasInlineArray)
                            pendingCalls = 8;
                    }
                }
            }

            if (op.OperandType == OperandType.InlineMethod && operandStart + 4 <= il.Length)
            {
                var token = BitConverter.ToInt32(il, operandStart);
                var h = MetadataTokens.EntityHandle(token);

                if (pendingCalls > 0)
                {
                    consumers.Add(FormatMethodRef(reader, h));
                    pendingCalls--;
                }
            }

            i = operandStart + operandLen;
        }

        foreach (var c in consumers.OrderBy(x => x, StringComparer.Ordinal))
            yield return c;
    }

    private static string? TryFindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "Content.Client")) &&
                Directory.Exists(Path.Combine(dir.FullName, "RobustToolbox")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return null;
    }

    private static string DefaultAssemblyPath()
    {
        var root = TryFindRepoRoot();
        if (root == null)
            throw new InvalidOperationException("Could not locate repo root (expected Content.Client/ and RobustToolbox/). Pass assembly path as first argument.");

        return Path.Combine(root, "bin", "Content.Client", "Content.Client.dll");
    }

    private static string GetFullTypeName(MetadataReader reader, EntityHandle typeHandle)
    {
        return typeHandle.Kind switch
        {
            HandleKind.TypeDefinition => GetFullTypeNameDef(reader, (TypeDefinitionHandle) typeHandle),
            HandleKind.TypeReference => GetFullTypeNameRef(reader, (TypeReferenceHandle) typeHandle),
            _ => $"<{typeHandle.Kind}>"
        };
    }

    private static string GetFullTypeNameDef(MetadataReader reader, TypeDefinitionHandle handle)
    {
        var def = reader.GetTypeDefinition(handle);

        // Handle nested types by walking declaring-type chain.
        var name = reader.GetString(def.Name);
        var current = def;
        while (!current.GetDeclaringType().IsNil)
        {
            var parentHandle = current.GetDeclaringType();
            var parent = reader.GetTypeDefinition(parentHandle);
            name = $"{reader.GetString(parent.Name)}+{name}";
            current = parent;
        }

        var ns = reader.GetString(current.Namespace);
        return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
    }

    private static string GetFullTypeNameRef(MetadataReader reader, TypeReferenceHandle handle)
    {
        var tr = reader.GetTypeReference(handle);
        var ns = reader.GetString(tr.Namespace);
        var name = reader.GetString(tr.Name);
        return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
    }

    private static string GetAttributeTypeName(MetadataReader reader, CustomAttribute attribute)
    {
        var ctor = attribute.Constructor;

        EntityHandle typeHandle = ctor.Kind switch
        {
            HandleKind.MemberReference => reader.GetMemberReference((MemberReferenceHandle) ctor).Parent,
            HandleKind.MethodDefinition => reader.GetMethodDefinition((MethodDefinitionHandle) ctor).GetDeclaringType(),
            _ => default
        };

        if (typeHandle.IsNil)
            return "<unknown>";

        return GetFullTypeName(reader, typeHandle);
    }

    private static bool HasPrivateImplInlineArrayHelper(MetadataReader reader)
    {
        foreach (var tdHandle in reader.TypeDefinitions)
        {
            var td = reader.GetTypeDefinition(tdHandle);
            var name = reader.GetString(td.Name);
            if (name != "<PrivateImplementationDetails>")
                continue;

            foreach (var mdHandle in td.GetMethods())
            {
                var md = reader.GetMethodDefinition(mdHandle);
                var methodName = reader.GetString(md.Name);
                if (methodName.Contains("InlineArrayAsReadOnlySpan", StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        return false;
    }

    private static bool IsInlineArrayAsReadOnlySpanCall(MetadataReader reader, int metadataToken)
    {
        try
        {
            var handle = MetadataTokens.EntityHandle(metadataToken);
            if (handle.Kind is not (HandleKind.MemberReference or HandleKind.MethodDefinition))
                return false;

            string methodName;
            EntityHandle declaringType;

            if (handle.Kind == HandleKind.MemberReference)
            {
                var mr = reader.GetMemberReference((MemberReferenceHandle) handle);
                methodName = reader.GetString(mr.Name);
                declaringType = mr.Parent;
            }
            else
            {
                var md = reader.GetMethodDefinition((MethodDefinitionHandle) handle);
                methodName = reader.GetString(md.Name);
                declaringType = md.GetDeclaringType();
            }

            if (!methodName.Contains("InlineArrayAsReadOnlySpan", StringComparison.Ordinal))
                return false;

            var typeName = GetFullTypeName(reader, declaringType);
            return typeName == "<PrivateImplementationDetails>";
        }
        catch
        {
            return false;
        }
    }

    private static IEnumerable<(string Method, int Offset)> FindInlineArrayHelperCalls(PEReader pe, MetadataReader reader)
    {
        foreach (var tdHandle in reader.TypeDefinitions)
        {
            var typeName = GetFullTypeNameDef(reader, tdHandle);
            var td = reader.GetTypeDefinition(tdHandle);

            foreach (var mdHandle in td.GetMethods())
            {
                var md = reader.GetMethodDefinition(mdHandle);
                var rva = md.RelativeVirtualAddress;
                if (rva == 0)
                    continue; // abstract / extern

                var body = pe.GetMethodBody(rva);
                var il = body.GetILBytes();

                // Fast heuristic scan:
                // 0x28 = call, 0x6F = callvirt, both followed by 4-byte metadata token.
                for (var i = 0; i + 4 < il.Length; i++)
                {
                    var op = il[i];
                    if (op != 0x28 && op != 0x6F)
                        continue;

                    var token = il[i + 1] | (il[i + 2] << 8) | (il[i + 3] << 16) | (il[i + 4] << 24);
                    if (!IsInlineArrayAsReadOnlySpanCall(reader, token))
                        continue;

                    var methodName = reader.GetString(md.Name);
                    yield return ($"{typeName}.{methodName}", i);
                }
            }
        }
    }

    public static int Main(string[] args)
    {
        var assemblyPath = args.Length > 0 ? args[0] : DefaultAssemblyPath();
        assemblyPath = Path.GetFullPath(assemblyPath);

        if (!File.Exists(assemblyPath))
        {
            Console.Error.WriteLine($"Assembly not found: {assemblyPath}");
            return 2;
        }

        Console.WriteLine($"Inspecting: {assemblyPath}");

        using var stream = File.OpenRead(assemblyPath);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();

        Console.WriteLine($"Has <PrivateImplementationDetails>.InlineArrayAsReadOnlySpan: {HasPrivateImplInlineArrayHelper(reader)}");

        const string InlineArrayAttributeName = "System.Runtime.CompilerServices.InlineArrayAttribute";

        var inlineArrayTypes = new List<string>();
        foreach (var tdHandle in reader.TypeDefinitions)
        {
            var td = reader.GetTypeDefinition(tdHandle);
            foreach (var caHandle in td.GetCustomAttributes())
            {
                var ca = reader.GetCustomAttribute(caHandle);
                var attrType = GetAttributeTypeName(reader, ca);
                if (attrType == InlineArrayAttributeName)
                {
                    inlineArrayTypes.Add(GetFullTypeNameDef(reader, tdHandle));
                    break;
                }
            }
        }

        Console.WriteLine($"Types with [{InlineArrayAttributeName}]: {inlineArrayTypes.Count}");
        foreach (var t in inlineArrayTypes.OrderBy(x => x, StringComparer.Ordinal))
        {
            Console.WriteLine($"  - {t}");
        }

        var referencesInlineArray = reader.TypeReferences.Any(trHandle =>
        {
            var tr = reader.GetTypeReference(trHandle);
            var ns = reader.GetString(tr.Namespace);
            var name = reader.GetString(tr.Name);
            return ns == "System.Runtime.CompilerServices" && name == "InlineArrayAttribute";
        });

        Console.WriteLine($"References InlineArrayAttribute type: {referencesInlineArray}");

        var callSites = FindInlineArrayHelperCalls(pe, reader).Distinct().ToList();
        Console.WriteLine($"Methods calling InlineArrayAsReadOnlySpan: {callSites.Count}");
        foreach (var (method, offset) in callSites.OrderBy(x => x.Method, StringComparer.Ordinal))
        {
            Console.WriteLine($"  - {method} (IL+0x{offset:X})");
        }

        var provider = new TypeMarkProvider(reader);
        var inlineArrayUsers = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (var tdHandle in reader.TypeDefinitions)
        {
            var td = reader.GetTypeDefinition(tdHandle);
            var typeName = GetFullTypeNameDef(reader, tdHandle);

            foreach (var mdHandle in td.GetMethods())
            {
                var md = reader.GetMethodDefinition(mdHandle);
                var methodName = reader.GetString(md.Name);
                var fullMethod = $"{typeName}.{methodName}";

                var sig = md.DecodeSignature(provider, genericContext: null);
                if (sig.ReturnType.HasInlineArray || sig.ParameterTypes.Any(p => p.HasInlineArray))
                {
                    if (!inlineArrayUsers.TryGetValue(fullMethod, out var set))
                        inlineArrayUsers[fullMethod] = set = new HashSet<string>(StringComparer.Ordinal);

                    if (sig.ReturnType.HasInlineArray)
                        set.Add($"return: {sig.ReturnType.Display}");

                    foreach (var p in sig.ParameterTypes.Where(p => p.HasInlineArray))
                        set.Add($"param: {p.Display}");
                }

                var rva = md.RelativeVirtualAddress;
                if (rva == 0)
                    continue;

                var body = pe.GetMethodBody(rva);
                if (body.LocalSignature.IsNil)
                    continue;

                var ss = reader.GetStandaloneSignature(body.LocalSignature);
                var locals = ss.DecodeLocalSignature(provider, genericContext: null);
                foreach (var l in locals.Where(l => l.HasInlineArray))
                {
                    if (!inlineArrayUsers.TryGetValue(fullMethod, out var set))
                        inlineArrayUsers[fullMethod] = set = new HashSet<string>(StringComparer.Ordinal);
                    set.Add($"local: {l.Display}");
                }
            }
        }

        Console.WriteLine($"Methods referencing <>y__InlineArray*: {inlineArrayUsers.Count}");
        foreach (var (m, details) in inlineArrayUsers.OrderBy(x => x.Key, StringComparer.Ordinal).Take(200))
        {
            Console.WriteLine($"  - {m}");
            foreach (var d in details.OrderBy(x => x, StringComparer.Ordinal))
                Console.WriteLine($"      {d}");

            // Best-effort: guess which call consumes the inline-array local.
            // For state machine methods and large methods this is heuristic, but usually points at the culprit API.
            var lastDot = m.LastIndexOf('.');
            if (lastDot > 0)
            {
                var tName = m.Substring(0, lastDot);
                var mName = m.Substring(lastDot + 1);
                var consumers = GuessInlineArrayConsumers(pe, reader, tName, mName).ToList();
                if (consumers.Count > 0)
                {
                    Console.WriteLine("      consumers (heuristic):");
                    foreach (var c in consumers.Take(10))
                        Console.WriteLine($"        - {c}");
                }
            }
        }

        return 0;
    }
}
