// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld Contributors
// See AGPLv3.txt for details.

using System.Numerics;
using Content.Shared._Lua.SpaceHazards;
using Robust.Client.Graphics;

namespace Content.Client._Lua.SpaceHazards;

public static class PixelPlanetPalettes
{
    public const int Count = 9;

    private static readonly Vector3[][] LandRivers = Build9(
        V(0.388f, 0.670f, 0.247f, 0.231f, 0.490f, 0.309f, 0.184f, 0.341f, 0.325f, 0.156f, 0.207f, 0.250f,
            0.309f, 0.643f, 0.721f, 0.250f, 0.286f, 0.450f,
            0.960f, 1.000f, 0.909f, 0.874f, 0.878f, 0.909f, 0.407f, 0.435f, 0.600f, 0.250f, 0.286f, 0.450f),
        V(0.780f, 0.290f, 0.090f, 0.600f, 0.190f, 0.090f, 0.440f, 0.140f, 0.090f, 0.240f, 0.090f, 0.070f,
            0.960f, 0.520f, 0.180f, 0.700f, 0.280f, 0.100f,
            1.000f, 0.900f, 0.700f, 0.900f, 0.700f, 0.500f, 0.600f, 0.380f, 0.200f, 0.400f, 0.200f, 0.150f),
        V(0.700f, 0.800f, 0.900f, 0.500f, 0.620f, 0.800f, 0.310f, 0.420f, 0.700f, 0.160f, 0.220f, 0.520f,
            0.850f, 0.940f, 1.000f, 0.580f, 0.740f, 0.900f,
            1.000f, 1.000f, 1.000f, 0.900f, 0.950f, 1.000f, 0.660f, 0.780f, 0.940f, 0.480f, 0.600f, 0.860f),
        V(0.180f, 0.680f, 0.120f, 0.120f, 0.500f, 0.180f, 0.380f, 0.220f, 0.560f, 0.220f, 0.120f, 0.400f,
            0.720f, 1.000f, 0.200f, 0.460f, 0.760f, 0.100f,
            0.860f, 1.000f, 0.700f, 0.720f, 0.980f, 0.540f, 0.340f, 0.560f, 0.220f, 0.200f, 0.340f, 0.120f),
        V(0.420f, 0.420f, 0.460f, 0.300f, 0.300f, 0.360f, 0.210f, 0.210f, 0.280f, 0.130f, 0.130f, 0.200f,
            0.460f, 0.480f, 0.620f, 0.280f, 0.290f, 0.440f,
            0.900f, 0.900f, 0.920f, 0.720f, 0.720f, 0.760f, 0.440f, 0.440f, 0.520f, 0.240f, 0.240f, 0.340f),
        V(0.420f, 0.440f, 0.380f, 0.280f, 0.300f, 0.320f, 0.180f, 0.190f, 0.220f, 0.120f, 0.130f, 0.160f,
            0.720f, 0.380f, 0.220f, 0.480f, 0.220f, 0.120f,
            0.920f, 0.930f, 0.950f, 0.650f, 0.670f, 0.720f, 0.380f, 0.420f, 0.500f, 0.220f, 0.240f, 0.300f),
        V(0.960f, 0.970f, 0.990f, 0.780f, 0.820f, 0.880f, 0.550f, 0.620f, 0.720f, 0.350f, 0.420f, 0.550f,
            0.250f, 0.450f, 0.920f, 0.150f, 0.280f, 0.650f,
            1.000f, 1.000f, 1.000f, 0.850f, 0.880f, 0.950f, 0.500f, 0.580f, 0.720f, 0.300f, 0.380f, 0.550f),
        V(0.850f, 0.620f, 0.380f, 0.680f, 0.450f, 0.280f, 0.480f, 0.300f, 0.180f, 0.320f, 0.200f, 0.120f,
            0.550f, 0.380f, 0.240f, 0.380f, 0.250f, 0.150f,
            0.960f, 0.880f, 0.720f, 0.820f, 0.720f, 0.550f, 0.550f, 0.420f, 0.300f, 0.380f, 0.280f, 0.180f),
        V(0.080f, 0.220f, 0.280f, 0.060f, 0.180f, 0.240f, 0.050f, 0.140f, 0.200f, 0.040f, 0.100f, 0.140f,
            0.200f, 0.850f, 0.780f, 0.080f, 0.450f, 0.520f,
            0.120f, 0.350f, 0.380f, 0.080f, 0.280f, 0.320f, 0.050f, 0.150f, 0.180f, 0.030f, 0.080f, 0.100f));

    private static readonly Vector3[][] IceWorld = Build9(
        V(0.980f, 1.000f, 1.000f, 0.780f, 0.830f, 0.880f, 0.570f, 0.560f, 0.720f,
            0.309f, 0.643f, 0.721f, 0.298f, 0.407f, 0.521f, 0.227f, 0.247f, 0.368f,
            0.882f, 0.949f, 1.000f, 0.752f, 0.890f, 1.000f, 0.368f, 0.439f, 0.647f, 0.250f, 0.286f, 0.450f),
        V(0.900f, 0.500f, 0.300f, 0.720f, 0.340f, 0.200f, 0.540f, 0.230f, 0.150f,
            0.960f, 0.600f, 0.250f, 0.780f, 0.380f, 0.150f, 0.540f, 0.220f, 0.120f,
            1.000f, 0.900f, 0.700f, 0.900f, 0.750f, 0.550f, 0.650f, 0.400f, 0.230f, 0.450f, 0.230f, 0.130f),
        V(0.960f, 0.980f, 1.000f, 0.820f, 0.880f, 0.960f, 0.640f, 0.720f, 0.900f,
            0.540f, 0.800f, 0.980f, 0.340f, 0.560f, 0.840f, 0.200f, 0.320f, 0.640f,
            1.000f, 1.000f, 1.000f, 0.920f, 0.960f, 1.000f, 0.600f, 0.740f, 0.960f, 0.380f, 0.520f, 0.820f),
        V(0.560f, 0.920f, 0.380f, 0.360f, 0.720f, 0.240f, 0.580f, 0.280f, 0.720f,
            0.680f, 0.980f, 0.180f, 0.480f, 0.760f, 0.120f, 0.300f, 0.480f, 0.080f,
            0.840f, 1.000f, 0.600f, 0.680f, 0.960f, 0.440f, 0.320f, 0.520f, 0.200f, 0.180f, 0.300f, 0.100f),
        V(0.820f, 0.820f, 0.860f, 0.640f, 0.640f, 0.700f, 0.460f, 0.460f, 0.540f,
            0.460f, 0.500f, 0.620f, 0.320f, 0.360f, 0.500f, 0.200f, 0.220f, 0.380f,
            0.920f, 0.920f, 0.940f, 0.780f, 0.780f, 0.820f, 0.500f, 0.500f, 0.600f, 0.300f, 0.300f, 0.420f),
        V(0.940f, 0.950f, 0.970f, 0.720f, 0.740f, 0.780f, 0.450f, 0.500f, 0.580f,
            0.350f, 0.550f, 0.620f, 0.220f, 0.380f, 0.480f, 0.140f, 0.240f, 0.340f,
            0.900f, 0.920f, 0.960f, 0.550f, 0.600f, 0.680f, 0.320f, 0.360f, 0.440f, 0.180f, 0.200f, 0.260f),
        V(0.990f, 0.995f, 1.000f, 0.880f, 0.910f, 0.960f, 0.680f, 0.740f, 0.880f,
            0.400f, 0.650f, 0.980f, 0.280f, 0.480f, 0.820f, 0.160f, 0.320f, 0.620f,
            1.000f, 1.000f, 1.000f, 0.900f, 0.930f, 1.000f, 0.580f, 0.680f, 0.920f, 0.380f, 0.500f, 0.780f),
        V(0.920f, 0.800f, 0.620f, 0.780f, 0.580f, 0.420f, 0.580f, 0.380f, 0.260f,
            0.720f, 0.520f, 0.360f, 0.520f, 0.340f, 0.220f, 0.340f, 0.220f, 0.140f,
            1.000f, 0.920f, 0.780f, 0.900f, 0.760f, 0.580f, 0.620f, 0.460f, 0.320f, 0.420f, 0.300f, 0.200f),
        V(0.450f, 0.720f, 0.680f, 0.280f, 0.520f, 0.560f, 0.160f, 0.360f, 0.440f,
            0.220f, 0.620f, 0.580f, 0.120f, 0.420f, 0.480f, 0.060f, 0.260f, 0.360f,
            0.520f, 0.880f, 0.820f, 0.320f, 0.680f, 0.720f, 0.160f, 0.420f, 0.520f, 0.080f, 0.240f, 0.380f));

    private static readonly Vector3[][] GasPlanet = Build9(
        V(0.231f, 0.125f, 0.152f, 0.231f, 0.125f, 0.152f, 0.129f, 0.094f, 0.105f, 0.129f, 0.094f, 0.105f,
            0.941f, 0.709f, 0.254f, 0.811f, 0.458f, 0.168f, 0.670f, 0.317f, 0.188f, 0.490f, 0.219f, 0.200f),
        V(0.500f, 0.130f, 0.090f, 0.500f, 0.130f, 0.090f, 0.280f, 0.080f, 0.060f, 0.280f, 0.080f, 0.060f,
            0.960f, 0.480f, 0.150f, 0.820f, 0.320f, 0.100f, 0.680f, 0.220f, 0.090f, 0.500f, 0.150f, 0.080f),
        V(0.120f, 0.200f, 0.480f, 0.120f, 0.200f, 0.480f, 0.080f, 0.120f, 0.320f, 0.080f, 0.120f, 0.320f,
            0.640f, 0.820f, 1.000f, 0.440f, 0.680f, 0.960f, 0.280f, 0.480f, 0.840f, 0.160f, 0.300f, 0.680f),
        V(0.280f, 0.120f, 0.500f, 0.280f, 0.120f, 0.500f, 0.180f, 0.080f, 0.340f, 0.180f, 0.080f, 0.340f,
            0.580f, 0.960f, 0.180f, 0.420f, 0.780f, 0.120f, 0.300f, 0.580f, 0.080f, 0.200f, 0.380f, 0.060f),
        V(0.200f, 0.200f, 0.260f, 0.200f, 0.200f, 0.260f, 0.120f, 0.120f, 0.180f, 0.120f, 0.120f, 0.180f,
            0.580f, 0.580f, 0.640f, 0.440f, 0.440f, 0.520f, 0.320f, 0.320f, 0.420f, 0.200f, 0.200f, 0.320f),
        V(0.180f, 0.140f, 0.160f, 0.180f, 0.140f, 0.160f, 0.100f, 0.080f, 0.090f, 0.100f, 0.080f, 0.090f,
            0.750f, 0.550f, 0.280f, 0.620f, 0.380f, 0.180f, 0.450f, 0.280f, 0.140f, 0.320f, 0.200f, 0.120f),
        V(0.450f, 0.580f, 0.720f, 0.450f, 0.580f, 0.720f, 0.280f, 0.380f, 0.520f, 0.280f, 0.380f, 0.520f,
            0.920f, 0.960f, 1.000f, 0.720f, 0.840f, 1.000f, 0.520f, 0.680f, 0.920f, 0.320f, 0.480f, 0.720f),
        V(0.420f, 0.220f, 0.140f, 0.420f, 0.220f, 0.140f, 0.280f, 0.140f, 0.090f, 0.280f, 0.140f, 0.090f,
            0.920f, 0.620f, 0.280f, 0.780f, 0.480f, 0.180f, 0.620f, 0.340f, 0.120f, 0.460f, 0.240f, 0.080f),
        V(0.060f, 0.140f, 0.220f, 0.060f, 0.140f, 0.220f, 0.040f, 0.090f, 0.150f, 0.040f, 0.090f, 0.150f,
            0.180f, 0.720f, 0.680f, 0.120f, 0.520f, 0.560f, 0.080f, 0.340f, 0.440f, 0.050f, 0.200f, 0.320f));

    private static readonly Vector3[][] LandMasses = Build9(
        V(0.572f, 0.909f, 0.752f, 0.309f, 0.643f, 0.721f, 0.172f, 0.207f, 0.301f,
            0.784f, 0.831f, 0.364f, 0.388f, 0.670f, 0.247f, 0.184f, 0.341f, 0.325f, 0.156f, 0.207f, 0.250f,
            0.874f, 0.878f, 0.909f, 0.639f, 0.654f, 0.760f, 0.407f, 0.435f, 0.600f, 0.250f, 0.286f, 0.450f),
        V(0.860f, 0.450f, 0.180f, 0.700f, 0.280f, 0.100f, 0.480f, 0.160f, 0.080f,
            0.900f, 0.600f, 0.250f, 0.750f, 0.380f, 0.150f, 0.540f, 0.230f, 0.120f, 0.350f, 0.130f, 0.080f,
            1.000f, 0.900f, 0.700f, 0.900f, 0.720f, 0.500f, 0.650f, 0.420f, 0.230f, 0.430f, 0.240f, 0.140f),
        V(0.460f, 0.720f, 0.960f, 0.260f, 0.520f, 0.860f, 0.140f, 0.280f, 0.600f,
            0.700f, 0.820f, 0.940f, 0.480f, 0.660f, 0.880f, 0.300f, 0.460f, 0.760f, 0.160f, 0.260f, 0.560f,
            1.000f, 1.000f, 1.000f, 0.880f, 0.930f, 1.000f, 0.580f, 0.720f, 0.960f, 0.360f, 0.500f, 0.820f),
        V(0.200f, 0.780f, 0.500f, 0.120f, 0.560f, 0.380f, 0.480f, 0.200f, 0.580f,
            0.680f, 0.960f, 0.200f, 0.460f, 0.760f, 0.120f, 0.320f, 0.520f, 0.080f, 0.200f, 0.320f, 0.060f,
            0.840f, 1.000f, 0.620f, 0.680f, 0.960f, 0.440f, 0.320f, 0.520f, 0.200f, 0.200f, 0.320f, 0.100f),
        V(0.480f, 0.520f, 0.620f, 0.340f, 0.380f, 0.500f, 0.200f, 0.220f, 0.360f,
            0.640f, 0.640f, 0.700f, 0.460f, 0.460f, 0.540f, 0.320f, 0.320f, 0.420f, 0.180f, 0.180f, 0.280f,
            0.900f, 0.900f, 0.920f, 0.740f, 0.740f, 0.780f, 0.500f, 0.500f, 0.600f, 0.300f, 0.300f, 0.420f),
        V(0.480f, 0.520f, 0.440f, 0.300f, 0.480f, 0.440f, 0.180f, 0.280f, 0.300f,
            0.680f, 0.480f, 0.280f, 0.550f, 0.350f, 0.200f, 0.420f, 0.220f, 0.140f, 0.280f, 0.140f, 0.100f,
            0.880f, 0.890f, 0.910f, 0.580f, 0.600f, 0.650f, 0.350f, 0.380f, 0.450f, 0.200f, 0.220f, 0.280f),
        V(0.820f, 0.900f, 0.980f, 0.580f, 0.720f, 0.880f, 0.320f, 0.480f, 0.680f,
            0.680f, 0.780f, 0.920f, 0.480f, 0.620f, 0.840f, 0.280f, 0.420f, 0.660f, 0.160f, 0.280f, 0.520f,
            1.000f, 1.000f, 1.000f, 0.880f, 0.910f, 0.980f, 0.560f, 0.660f, 0.880f, 0.360f, 0.480f, 0.720f),
        V(0.880f, 0.680f, 0.420f, 0.720f, 0.480f, 0.280f, 0.480f, 0.280f, 0.160f,
            0.820f, 0.580f, 0.320f, 0.640f, 0.420f, 0.220f, 0.440f, 0.280f, 0.140f, 0.280f, 0.180f, 0.100f,
            0.980f, 0.900f, 0.760f, 0.860f, 0.720f, 0.560f, 0.600f, 0.460f, 0.340f, 0.420f, 0.320f, 0.220f),
        V(0.120f, 0.420f, 0.480f, 0.080f, 0.320f, 0.400f, 0.050f, 0.200f, 0.300f,
            0.220f, 0.580f, 0.520f, 0.140f, 0.420f, 0.460f, 0.080f, 0.280f, 0.380f, 0.050f, 0.180f, 0.280f,
            0.280f, 0.780f, 0.720f, 0.180f, 0.580f, 0.620f, 0.100f, 0.380f, 0.480f, 0.060f, 0.240f, 0.360f));

    private static readonly Vector3[][] LavaWorld = Build9(
        V(0.560f, 0.301f, 0.341f, 0.321f, 0.200f, 0.247f, 0.239f, 0.160f, 0.211f,
            0.321f, 0.200f, 0.247f, 0.239f, 0.160f, 0.211f,
            1.000f, 0.537f, 0.200f, 0.901f, 0.270f, 0.223f, 0.678f, 0.184f, 0.270f),
        V(0.700f, 0.200f, 0.100f, 0.500f, 0.130f, 0.090f, 0.350f, 0.100f, 0.080f,
            0.480f, 0.130f, 0.090f, 0.320f, 0.090f, 0.070f,
            1.000f, 0.800f, 0.100f, 1.000f, 0.520f, 0.100f, 0.880f, 0.280f, 0.100f),
        V(0.400f, 0.520f, 0.720f, 0.260f, 0.380f, 0.620f, 0.160f, 0.240f, 0.480f,
            0.240f, 0.360f, 0.600f, 0.140f, 0.220f, 0.440f,
            0.200f, 0.900f, 1.000f, 0.100f, 0.650f, 0.960f, 0.060f, 0.420f, 0.780f),
        V(0.300f, 0.620f, 0.200f, 0.200f, 0.460f, 0.140f, 0.140f, 0.300f, 0.100f,
            0.200f, 0.460f, 0.140f, 0.140f, 0.300f, 0.100f,
            0.820f, 1.000f, 0.100f, 0.600f, 0.860f, 0.060f, 0.380f, 0.620f, 0.040f),
        V(0.360f, 0.360f, 0.400f, 0.240f, 0.240f, 0.300f, 0.160f, 0.160f, 0.220f,
            0.240f, 0.240f, 0.300f, 0.160f, 0.160f, 0.220f,
            0.760f, 0.760f, 0.820f, 0.580f, 0.580f, 0.660f, 0.400f, 0.400f, 0.480f),
        V(0.400f, 0.350f, 0.320f, 0.280f, 0.240f, 0.220f, 0.180f, 0.150f, 0.140f,
            0.320f, 0.280f, 0.250f, 0.220f, 0.180f, 0.160f,
            1.000f, 0.750f, 0.250f, 0.950f, 0.450f, 0.120f, 0.720f, 0.220f, 0.080f),
        V(0.520f, 0.580f, 0.680f, 0.380f, 0.440f, 0.560f, 0.260f, 0.320f, 0.460f,
            0.360f, 0.420f, 0.540f, 0.240f, 0.300f, 0.440f,
            1.000f, 0.520f, 0.120f, 0.980f, 0.320f, 0.080f, 0.780f, 0.180f, 0.050f),
        V(0.620f, 0.380f, 0.240f, 0.420f, 0.260f, 0.160f, 0.280f, 0.170f, 0.110f,
            0.420f, 0.260f, 0.160f, 0.280f, 0.170f, 0.110f,
            1.000f, 0.720f, 0.180f, 0.980f, 0.480f, 0.080f, 0.820f, 0.280f, 0.040f),
        V(0.100f, 0.280f, 0.320f, 0.070f, 0.200f, 0.260f, 0.050f, 0.140f, 0.200f,
            0.080f, 0.220f, 0.280f, 0.050f, 0.160f, 0.220f,
            0.400f, 1.000f, 0.920f, 0.120f, 0.820f, 0.780f, 0.060f, 0.520f, 0.580f));

    private static readonly Vector3[][] NoAtmosphere = Build9(
        V(0.639f, 0.654f, 0.760f, 0.298f, 0.407f, 0.521f, 0.227f, 0.247f, 0.368f, 0.298f, 0.407f, 0.521f, 0.227f, 0.247f, 0.368f),
        V(0.760f, 0.340f, 0.150f, 0.560f, 0.210f, 0.100f, 0.380f, 0.130f, 0.080f, 0.560f, 0.210f, 0.100f, 0.380f, 0.130f, 0.080f),
        V(0.780f, 0.860f, 0.980f, 0.580f, 0.700f, 0.900f, 0.380f, 0.500f, 0.780f, 0.580f, 0.700f, 0.900f, 0.380f, 0.500f, 0.780f),
        V(0.340f, 0.720f, 0.260f, 0.220f, 0.520f, 0.180f, 0.500f, 0.220f, 0.640f, 0.220f, 0.520f, 0.180f, 0.500f, 0.220f, 0.640f),
        V(0.600f, 0.600f, 0.660f, 0.400f, 0.400f, 0.480f, 0.240f, 0.240f, 0.340f, 0.400f, 0.400f, 0.480f, 0.240f, 0.240f, 0.340f),
        V(0.620f, 0.620f, 0.650f, 0.420f, 0.420f, 0.460f, 0.240f, 0.240f, 0.280f, 0.420f, 0.420f, 0.460f, 0.240f, 0.240f, 0.280f),
        V(0.820f, 0.860f, 0.940f, 0.580f, 0.640f, 0.800f, 0.380f, 0.460f, 0.640f, 0.580f, 0.640f, 0.800f, 0.380f, 0.460f, 0.640f),
        V(0.720f, 0.540f, 0.380f, 0.520f, 0.360f, 0.240f, 0.340f, 0.220f, 0.140f, 0.520f, 0.360f, 0.240f, 0.340f, 0.220f, 0.140f),
        V(0.180f, 0.380f, 0.420f, 0.100f, 0.260f, 0.320f, 0.060f, 0.160f, 0.220f, 0.100f, 0.260f, 0.320f, 0.060f, 0.160f, 0.220f));

    private static readonly Vector3[][] TerranDry = Build9(
        V(1.000f, 0.537f, 0.200f, 0.898f, 0.266f, 0.219f, 0.674f, 0.184f, 0.266f, 0.317f, 0.196f, 0.243f, 0.239f, 0.156f, 0.211f),
        V(1.000f, 0.650f, 0.150f, 0.940f, 0.400f, 0.100f, 0.760f, 0.220f, 0.090f, 0.540f, 0.150f, 0.080f, 0.350f, 0.100f, 0.060f),
        V(0.820f, 0.920f, 1.000f, 0.600f, 0.760f, 0.960f, 0.380f, 0.560f, 0.880f, 0.200f, 0.360f, 0.740f, 0.100f, 0.200f, 0.560f),
        V(0.800f, 1.000f, 0.200f, 0.560f, 0.820f, 0.120f, 0.380f, 0.580f, 0.080f, 0.480f, 0.200f, 0.620f, 0.300f, 0.120f, 0.440f),
        V(0.780f, 0.780f, 0.840f, 0.620f, 0.620f, 0.700f, 0.460f, 0.460f, 0.540f, 0.300f, 0.300f, 0.400f, 0.180f, 0.180f, 0.280f),
        V(0.780f, 0.520f, 0.280f, 0.620f, 0.380f, 0.220f, 0.480f, 0.260f, 0.140f, 0.340f, 0.180f, 0.100f, 0.220f, 0.120f, 0.070f),
        V(0.920f, 0.940f, 1.000f, 0.680f, 0.760f, 0.920f, 0.440f, 0.560f, 0.780f, 0.280f, 0.380f, 0.580f, 0.160f, 0.240f, 0.440f),
        V(1.000f, 0.680f, 0.280f, 0.920f, 0.520f, 0.180f, 0.760f, 0.360f, 0.120f, 0.540f, 0.260f, 0.100f, 0.360f, 0.180f, 0.080f),
        V(0.220f, 0.680f, 0.620f, 0.140f, 0.520f, 0.480f, 0.080f, 0.360f, 0.400f, 0.120f, 0.280f, 0.380f, 0.080f, 0.180f, 0.260f));

    private static readonly Vector3[][] Star = Build9(
        V(0.961f, 1.000f, 0.910f, 0.467f, 0.839f, 0.757f, 0.110f, 0.573f, 0.655f, 0.012f, 0.243f, 0.369f),
        V(1.000f, 0.900f, 0.700f, 1.000f, 0.600f, 0.200f, 0.860f, 0.280f, 0.100f, 0.500f, 0.130f, 0.080f),
        V(0.900f, 0.960f, 1.000f, 0.560f, 0.800f, 1.000f, 0.240f, 0.560f, 0.920f, 0.080f, 0.260f, 0.680f),
        V(0.880f, 1.000f, 0.600f, 0.520f, 0.920f, 0.200f, 0.280f, 0.620f, 0.080f, 0.400f, 0.160f, 0.560f),
        V(0.920f, 0.920f, 0.940f, 0.680f, 0.680f, 0.740f, 0.440f, 0.440f, 0.520f, 0.200f, 0.200f, 0.320f),
        V(1.000f, 0.980f, 0.880f, 0.820f, 0.780f, 0.620f, 0.520f, 0.480f, 0.420f, 0.280f, 0.240f, 0.200f),
        V(1.000f, 1.000f, 1.000f, 0.720f, 0.840f, 1.000f, 0.380f, 0.620f, 1.000f, 0.120f, 0.380f, 0.820f),
        V(1.000f, 0.920f, 0.720f, 1.000f, 0.680f, 0.280f, 0.880f, 0.420f, 0.120f, 0.520f, 0.220f, 0.080f),
        V(0.720f, 1.000f, 0.920f, 0.120f, 0.720f, 0.820f, 0.040f, 0.420f, 0.620f, 0.020f, 0.180f, 0.420f));

    private static readonly Vector3[][] StarBlobs = Build9(
        V(1.000f, 1.000f, 0.894f),
        V(1.000f, 0.700f, 0.250f),
        V(0.800f, 0.940f, 1.000f),
        V(0.720f, 1.000f, 0.200f),
        V(0.860f, 0.860f, 0.900f),
        V(0.950f, 0.930f, 0.880f),
        V(0.880f, 0.940f, 1.000f),
        V(1.000f, 0.820f, 0.480f),
        V(0.400f, 0.950f, 0.880f));

    private static readonly Vector3[][] StarFlares = Build9(
        V(0.467f, 0.839f, 0.757f, 1.000f, 1.000f, 0.894f),
        V(1.000f, 0.600f, 0.200f, 1.000f, 0.900f, 0.600f),
        V(0.560f, 0.800f, 1.000f, 0.900f, 0.960f, 1.000f),
        V(0.520f, 0.920f, 0.200f, 0.880f, 1.000f, 0.600f),
        V(0.680f, 0.680f, 0.740f, 0.920f, 0.920f, 0.940f),
        V(0.720f, 0.760f, 0.820f, 0.920f, 0.780f, 0.420f),
        V(0.450f, 0.720f, 1.000f, 0.920f, 0.960f, 1.000f),
        V(1.000f, 0.720f, 0.320f, 1.000f, 0.920f, 0.680f),
        V(0.120f, 0.820f, 0.880f, 0.600f, 1.000f, 0.950f));

    private static readonly Vector3[][] BlackHole = Build9(
        V(0.153f, 0.153f, 0.212f, 1.000f, 1.000f, 0.922f, 0.929f, 0.482f, 0.224f,
            1.000f, 1.000f, 0.922f, 1.000f, 0.961f, 0.251f, 1.000f, 0.722f, 0.290f, 0.929f, 0.482f, 0.224f, 0.741f, 0.251f, 0.208f),
        V(0.250f, 0.100f, 0.060f, 1.000f, 0.900f, 0.700f, 1.000f, 0.520f, 0.100f,
            1.000f, 0.900f, 0.700f, 1.000f, 0.700f, 0.300f, 1.000f, 0.520f, 0.130f, 0.880f, 0.320f, 0.100f, 0.680f, 0.200f, 0.100f),
        V(0.060f, 0.120f, 0.360f, 0.900f, 0.960f, 1.000f, 0.260f, 0.700f, 1.000f,
            0.900f, 0.960f, 1.000f, 0.620f, 0.840f, 1.000f, 0.360f, 0.680f, 1.000f, 0.180f, 0.480f, 0.920f, 0.080f, 0.280f, 0.720f),
        V(0.120f, 0.280f, 0.080f, 0.880f, 1.000f, 0.600f, 0.600f, 0.960f, 0.100f,
            0.880f, 1.000f, 0.600f, 0.720f, 1.000f, 0.200f, 0.560f, 0.880f, 0.100f, 0.400f, 0.680f, 0.080f, 0.500f, 0.200f, 0.720f),
        V(0.100f, 0.100f, 0.180f, 0.900f, 0.900f, 0.920f, 0.680f, 0.680f, 0.740f,
            0.900f, 0.900f, 0.920f, 0.760f, 0.760f, 0.820f, 0.580f, 0.580f, 0.660f, 0.400f, 0.400f, 0.480f, 0.240f, 0.240f, 0.340f),
        V(0.080f, 0.090f, 0.120f, 0.920f, 0.920f, 0.940f, 0.780f, 0.550f, 0.280f,
            0.900f, 0.880f, 0.820f, 0.820f, 0.620f, 0.320f, 0.680f, 0.480f, 0.240f, 0.520f, 0.380f, 0.220f, 0.360f, 0.260f, 0.160f),
        V(0.120f, 0.220f, 0.420f, 0.950f, 0.970f, 1.000f, 0.320f, 0.620f, 1.000f,
            0.920f, 0.950f, 1.000f, 0.560f, 0.760f, 1.000f, 0.320f, 0.560f, 0.920f, 0.180f, 0.400f, 0.780f, 0.080f, 0.260f, 0.580f),
        V(0.220f, 0.120f, 0.080f, 0.960f, 0.880f, 0.720f, 0.920f, 0.520f, 0.180f,
            0.960f, 0.880f, 0.720f, 1.000f, 0.720f, 0.320f, 0.920f, 0.520f, 0.180f, 0.720f, 0.380f, 0.140f, 0.480f, 0.260f, 0.100f),
        V(0.020f, 0.060f, 0.100f, 0.400f, 0.880f, 0.820f, 0.080f, 0.520f, 0.680f,
            0.350f, 0.820f, 0.780f, 0.220f, 0.680f, 0.860f, 0.120f, 0.520f, 0.720f, 0.060f, 0.320f, 0.520f, 0.040f, 0.180f, 0.320f));

    public static int ClampIndex(int index) => Math.Clamp(index, 0, Count - 1);

    public static int CelestialIndex(byte palette) => ClampIndex(palette);

    public static void ApplyStarBlobs(ShaderInstance shader, byte palette)
    {
        var c = StarBlobs[CelestialIndex(palette)];
        shader.SetParameter("blob_color", c[0]);
    }

    public static void ApplyStar(ShaderInstance shader, byte palette)
    {
        var c = Star[CelestialIndex(palette)];
        shader.SetParameter("star_colors0", c[0]);
        shader.SetParameter("star_colors1", c[1]);
        shader.SetParameter("star_colors2", c[2]);
        shader.SetParameter("star_colors3", c[3]);
    }

    public static void ApplyStarFlares(ShaderInstance shader, byte palette)
    {
        var c = StarFlares[CelestialIndex(palette)];
        shader.SetParameter("flare_col0", c[0]);
        shader.SetParameter("flare_col1", c[1]);
    }

    public static void ApplyBlackHole(ShaderInstance shader, byte palette)
    {
        var c = BlackHole[CelestialIndex(palette)];
        shader.SetParameter("hole_c0", Vector3.Zero);
        shader.SetParameter("hole_c1", new Vector3(0.02f, 0.02f, 0.025f));
        shader.SetParameter("hole_c2", new Vector3(0.06f, 0.06f, 0.07f));
        shader.SetParameter("ring_c0", c[3]);
        shader.SetParameter("ring_c1", c[4]);
        shader.SetParameter("ring_c2", c[5]);
        shader.SetParameter("ring_c3", c[6]);
        shader.SetParameter("ring_c4", c[7]);
    }

    public static void ApplyPlanet(ShaderInstance shader, PixelPlanetKind kind, byte paletteIndex)
    {
        var i = ClampIndex(paletteIndex);
        switch (kind)
        {
            case PixelPlanetKind.LandRivers:
                {
                    var c = LandRivers[i];
                    shader.SetParameter("color0", c[0]);
                    shader.SetParameter("color1", c[1]);
                    shader.SetParameter("color2", c[2]);
                    shader.SetParameter("color3", c[3]);
                    shader.SetParameter("river_col", c[4]);
                    shader.SetParameter("river_col_dark", c[5]);
                    shader.SetParameter("cloud_base", c[6]);
                    shader.SetParameter("cloud_outline", c[7]);
                    shader.SetParameter("cloud_shadow_base", c[8]);
                    shader.SetParameter("cloud_shadow_outline", c[9]);
                    break;
                }
            case PixelPlanetKind.TerranDry:
                {
                    var c = TerranDry[i];
                    shader.SetParameter("color0", c[0]);
                    shader.SetParameter("color1", c[1]);
                    shader.SetParameter("color2", c[2]);
                    shader.SetParameter("color3", c[3]);
                    shader.SetParameter("color4", c[4]);
                    break;
                }
            case PixelPlanetKind.LandMasses:
                {
                    var c = LandMasses[i];
                    shader.SetParameter("color0", c[0]);
                    shader.SetParameter("color1", c[1]);
                    shader.SetParameter("color2", c[2]);
                    shader.SetParameter("land_color0", c[3]);
                    shader.SetParameter("land_color1", c[4]);
                    shader.SetParameter("land_color2", c[5]);
                    shader.SetParameter("land_color3", c[6]);
                    shader.SetParameter("cloud_base", c[7]);
                    shader.SetParameter("cloud_outline", c[8]);
                    shader.SetParameter("cloud_shadow_base", c[9]);
                    break;
                }
            case PixelPlanetKind.NoAtmosphere:
                {
                    var c = NoAtmosphere[i];
                    shader.SetParameter("color0", c[0]);
                    shader.SetParameter("color1", c[1]);
                    shader.SetParameter("color2", c[2]);
                    shader.SetParameter("crater_color0", c[3]);
                    shader.SetParameter("crater_color1", c[4]);
                    break;
                }
            case PixelPlanetKind.GasPlanet:
                {
                    var c = GasPlanet[i];
                    shader.SetParameter("inner_color0", c[0]);
                    shader.SetParameter("inner_color1", c[1]);
                    shader.SetParameter("inner_color2", c[2]);
                    shader.SetParameter("inner_color3", c[3]);
                    shader.SetParameter("color0", c[4]);
                    shader.SetParameter("color1", c[5]);
                    shader.SetParameter("color2", c[6]);
                    shader.SetParameter("color3", c[7]);
                    break;
                }
            case PixelPlanetKind.GasPlanetLayers:
                {
                    var c = GasPlanet[i];
                    shader.SetParameter("color0", c[4]);
                    shader.SetParameter("color1", c[5]);
                    shader.SetParameter("color2", c[6]);
                    shader.SetParameter("dark_color0", c[0]);
                    shader.SetParameter("dark_color1", c[1]);
                    shader.SetParameter("dark_color2", c[2]);
                    break;
                }
            case PixelPlanetKind.IceWorld:
                {
                    var c = IceWorld[i];
                    shader.SetParameter("color0", c[0]);
                    shader.SetParameter("color1", c[1]);
                    shader.SetParameter("color2", c[2]);
                    shader.SetParameter("lake_color0", c[3]);
                    shader.SetParameter("lake_color1", c[4]);
                    shader.SetParameter("lake_color2", c[5]);
                    shader.SetParameter("cloud_base", c[6]);
                    shader.SetParameter("cloud_outline", c[7]);
                    shader.SetParameter("cloud_shadow_base", c[8]);
                    shader.SetParameter("cloud_shadow_outline", c[9]);
                    break;
                }
            case PixelPlanetKind.LavaWorld:
                {
                    var c = LavaWorld[i];
                    shader.SetParameter("color0", c[0]);
                    shader.SetParameter("color1", c[1]);
                    shader.SetParameter("color2", c[2]);
                    shader.SetParameter("crater_color0", c[3]);
                    shader.SetParameter("crater_color1", c[4]);
                    shader.SetParameter("lava_color0", c[5]);
                    shader.SetParameter("lava_color1", c[6]);
                    shader.SetParameter("lava_color2", c[7]);
                    break;
                }
        }
    }

    private static Vector3[][] Build9(params Vector3[][] rows) => rows;

    private static Vector3[] V(params float[] rgb)
    {
        var result = new Vector3[rgb.Length / 3];
        for (var i = 0; i < result.Length; i++)
            result[i] = new Vector3(rgb[i * 3], rgb[i * 3 + 1], rgb[i * 3 + 2]);
        return result;
    }
}
