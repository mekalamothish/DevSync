import { readFileSync } from "node:fs";
import { spawn } from "node:child_process";

const args = process.argv.slice(2);
const isTest = args[0] === "--test";
if (isTest) args.shift();
if (!args.length) throw new Error("Provide a command to run.");

const values = {};
try {
    for (const line of readFileSync(".env", "utf8").split(/\r?\n/)) {
        if (!line.trim() || line.trim().startsWith("#")) continue;
        const match = line.match(/^([A-Z_]+)=(.*)$/);
        if (!match) throw new Error("Use KEY=value entries in .env.");
        values[match[1]] = match[2];
    }
} catch (error) {
    if (error.code === "ENOENT") throw new Error("Run npm run local:init before starting the database workflow.");
    throw error;
}

for (const key of ["POSTGRES_DB", "POSTGRES_USER", "POSTGRES_PASSWORD", "POSTGRES_PORT"]) {
    if (!values[key]) throw new Error(`Missing ${key} in .env.`);
}
if (!/^\d+$/.test(values.POSTGRES_PORT)) throw new Error("POSTGRES_PORT must be a number.");

const quote = (value) => `"${value.replaceAll('"', '""')}"`;
const database = isTest ? `${values.POSTGRES_DB}_tests` : values.POSTGRES_DB;
const connection = `Host=localhost;Port=${values.POSTGRES_PORT};Database=${quote(database)};Username=${quote(values.POSTGRES_USER)};Password=${quote(values.POSTGRES_PASSWORD)}`;
const env = { ...process.env, ConnectionStrings__DefaultConnection: connection };
if (isTest) env.DEVSYNC_TEST_CONNECTION = connection;

const child = spawn(args[0], args.slice(1), { env, stdio: "inherit", shell: false });
for (const signal of ["SIGINT", "SIGTERM"]) {
    process.on(signal, () => child.kill(signal));
}
child.on("error", (error) => { console.error(error.message); process.exitCode = 1; });
child.on("exit", (code, signal) => { process.exitCode = code ?? (signal ? 1 : 0); });
