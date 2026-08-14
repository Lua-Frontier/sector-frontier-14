const fs = require("fs");
const { parseChangelogBlocks } = require("./parse");

async function main() {
    const eventPath = process.env.GITHUB_EVENT_PATH;
    if (!eventPath) {
        console.error("GITHUB_EVENT_PATH not set.");
        process.exit(1);
    }
    const event = JSON.parse(fs.readFileSync(eventPath, "utf8"));
    const pullRequest = event.pull_request || {};
    const body = pullRequest.body || "";
    const defaultAuthor = pullRequest.user && pullRequest.user.login ? pullRequest.user.login : "unknown";

    const blocks = parseChangelogBlocks(body, defaultAuthor);
    if (blocks.length === 0) {
        console.log("No changelog entry found.");
        return;
    }

    let success = true;

    blocks.forEach((block, i) => {
        if (block.entries.length <= 0) {
            console.log(`Changelog block ${i + 1} has a header but no valid entries. Either remove the changelog completely, or use entries of the format '- add: text', '- remove: text', '- tweak: text', or '- fix: text'.`);
            success = false;
        }

        block.errors.forEach((entry) => {
            console.log(`Invalid changelog entry in block ${i + 1}: "${entry.type}" with message "${entry.message}"`);
            success = false;
        });
    });

    if (!success) {
        return process.exit(1);
    }

    console.log(`Changelog is valid (${blocks.length} ${blocks.length === 1 ? "entry" : "entries"}).`);
    blocks.forEach((block, i) => {
        const authorSource = block.namedAuthor ? "explicit" : "PR author";
        console.log(`\nBlock ${i + 1} — author: "${block.author}" (${authorSource})`);
        block.entries.forEach((entry) => {
            console.log(`  ${entry.type}: ${entry.message}`);
        });
    });
}

main();
