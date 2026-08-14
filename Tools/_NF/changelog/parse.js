const HeaderRegex = /^\s*(?::cl:|🆑)[ \t]*(.*)$/gimu;
const EntryRegex = /^ *[*-]? *(\w+): *([^\n\r]+)\r?$/gim;
const CommentRegex = /<!--.*?-->/gs;

const ChangeTypes = {
    add: "Add",
    remove: "Remove",
    tweak: "Tweak",
    fix: "Fix",
};

function stripComments(body) {
    return (body || "").replace(CommentRegex, "");
}

function getChanges(body) {
    const entries = [];
    const errors = [];

    for (const match of body.matchAll(EntryRegex)) {
        const rawType = match[1];
        const message = match[2].trim();
        const type = ChangeTypes[rawType.toLowerCase()];

        if (type) {
            entries.push({ type, message });
        } else {
            errors.push({ type: rawType, message });
        }
    }

    return { entries, errors };
}

function parseChangelogBlocks(body, defaultAuthor) {
    const text = stripComments(body);
    const headers = [...text.matchAll(new RegExp(HeaderRegex.source, HeaderRegex.flags))];

    return headers.map((header, i) => {
        const start = header.index + header[0].length;
        const end = i + 1 < headers.length ? headers[i + 1].index : text.length;
        const namedAuthor = (header[1] || "").trim();
        const { entries, errors } = getChanges(text.slice(start, end));

        return {
            author: namedAuthor || defaultAuthor,
            namedAuthor: namedAuthor || null,
            entries,
            errors,
        };
    });
}

module.exports = {
    parseChangelogBlocks,
    stripComments,
};
