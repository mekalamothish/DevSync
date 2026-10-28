import { readFileSync, mkdirSync, openSync, closeSync, unlinkSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { spawnSync } from 'node:child_process';

// Local Compose recovery only. Never restore over an existing database.
const [action, archiveArgument, destination] = process.argv.slice(2);
if (!['backup', 'restore-test'].includes(action) || !archiveArgument)
    throw new Error('Usage: node scripts/database-recovery.mjs backup ARCHIVE | restore-test ARCHIVE NEW_DATABASE_restore_tests');
const values = Object.fromEntries(readFileSync('.env', 'utf8').split(/\r?\n/)
    .filter(line => line.trim() && !line.trim().startsWith('#')).map(line => {
        const split = line.indexOf('=');
        if (split < 1) throw new Error('Use KEY=value entries in .env.');
        return [line.slice(0, split), line.slice(split + 1)];
    }));
const database = values.POSTGRES_DB || 'devsync';
const user = values.POSTGRES_USER || 'devsync';
const archive = resolve(archiveArgument);
const compose = ['compose', 'exec', '-T', 'postgres'];
function run(args, stdio = 'inherit') {
    const result = spawnSync('docker', [...compose, ...args], { shell: false, stdio });
    if (result.error) throw result.error;
    if (result.status !== 0) throw new Error('Database recovery command failed; inspect the preceding diagnostic.');
}
if (action === 'backup') {
    mkdirSync(dirname(archive), { recursive: true });
    const fd = openSync(archive, 'wx', 0o600);
    try {
        run(['pg_dump', '-U', user, '-d', database, '--format=custom', '--no-owner', '--no-privileges'], ['ignore', fd, 'inherit']);
    } catch (error) {
        closeSync(fd); unlinkSync(archive); throw error;
    }
    closeSync(fd);
    console.log(`Backup created: ${archive}`);
} else {
    if (!destination || !/^[a-z][a-z0-9_]{0,48}_restore_tests$/.test(destination) || destination === database)
        throw new Error('Restore requires a new database name ending in _restore_tests.');
    const fd = openSync(archive, 'r');
    try {
        // createdb fails if the destination already exists; no drop/clean/overwrite path.
        run(['createdb', '-U', user, '-T', 'template0', destination]);
        run(['pg_restore', '-U', user, '-d', destination, '--single-transaction', '--exit-on-error', '--no-owner', '--no-privileges'], [fd, 'inherit', 'inherit']);
    } finally { closeSync(fd); }
    console.log(`Restored into isolated database: ${destination}`);
}
