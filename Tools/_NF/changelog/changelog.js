const fs = require("fs");
const yaml = require("js-yaml");
const axios = require("axios");
const { parseChangelogBlocks } = require("./parse");

if (process.env.GITHUB_TOKEN) axios.defaults.headers.common["Authorization"] = `Bearer ${process.env.GITHUB_TOKEN}`;

if (!process.env.CHANGELOG_DIR) {
    console.log("CHANGELOG_DIR not defined, exiting.");
    return process.exit(1);
}

const ChangelogFilePath = `../../../${process.env.CHANGELOG_DIR}`;

async function main() {
    const pr = await axios.get(`https://api.github.com/repos/${process.env.GITHUB_REPOSITORY}/pulls/${process.env.PR_NUMBER}`);
    const { merged_at, body, user } = pr.data;

    const blocks = parseChangelogBlocks(body, user.login);
    if (blocks.length === 0) {
        console.log("No changelog entry found, skipping");
        return;
    }

    let time = merged_at;
    if (time) {
        time = time.replace("z", ".0000000+00:00").replace("Z", ".0000000+00:00");
    } else {
        console.log("Pull request was not merged, skipping");
        return;
    }

    const url = `https://github.com/${process.env.GITHUB_REPOSITORY}/pull/${process.env.PR_NUMBER}`;
    let nextId = getHighestCLNumber() + 1;
    const newEntries = [];

    for (const block of blocks) {
        if (block.entries.length === 0) {
            console.log(`Changelog block for "${block.author}" has no valid entries, skipping`);
            continue;
        }

        if (!block.namedAuthor) {
            console.log(`No author found for a changelog block, setting it to author of the PR (${user.login})`);
        }

        newEntries.push({
            author: block.author,
            changes: block.entries,
            id: nextId++,
            time,
            url,
        });
    }

    if (newEntries.length === 0) {
        console.log("No valid changelog entries found, skipping");
        return;
    }

    writeChangelogs(newEntries);
    console.log(`Changelog updated with ${newEntries.length} ${newEntries.length === 1 ? "entry" : "entries"} from PR #${process.env.PR_NUMBER}`);
}

function getHighestCLNumber() {
    if (!fs.existsSync(ChangelogFilePath)) {
        return 0;
    }

    const file = fs.readFileSync(ChangelogFilePath, "utf8");
    const data = yaml.load(file);
    const entries = data && data.Entries ? Array.from(data.Entries) : [];
    const clNumbers = entries.map((entry) => entry.id);

    return Math.max(...clNumbers, 0);
}

function writeChangelogs(newEntries) {
    let data = { Entries: [] };

    if (fs.existsSync(ChangelogFilePath)) {
        const file = fs.readFileSync(ChangelogFilePath, "utf8");
        data = yaml.load(file) || { Entries: [] };
        data.Entries = data.Entries || [];
    }

    data.Entries.push(...newEntries);

    fs.writeFileSync(
        ChangelogFilePath,
        "Entries:\n" +
            yaml.dump(data.Entries, { indent: 2 }).replace(/^---/, "")
    );
}

main();
