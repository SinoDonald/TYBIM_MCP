// Adapted from upstream schema. Node 24 built-in SQLite avoids another native
// better-sqlite3 binary. Open lazily so discovery does not create user data.
import { DatabaseSync } from 'node:sqlite';
import { mkdirSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { homedir } from 'node:os';
let native: DatabaseSync | undefined;
export function databasePath(): string {
  return process.env.TYBIM_UPSTREAM_DB || join(process.env.TYBIM_CHAT_STATE || join(process.env.LOCALAPPDATA || homedir(), 'TYBIM', 'McpChat'), 'upstream-data.db');
}
function getDb(): DatabaseSync {
  if (native) return native;
  const path = databasePath(); mkdirSync(dirname(path), { recursive: true });
  native = new DatabaseSync(path);
  native.exec(`PRAGMA foreign_keys = ON;
    CREATE TABLE IF NOT EXISTS projects (
      id INTEGER PRIMARY KEY AUTOINCREMENT, project_name TEXT NOT NULL UNIQUE,
      project_path TEXT, project_number TEXT, project_address TEXT, client_name TEXT,
      project_status TEXT, author TEXT, timestamp INTEGER NOT NULL, last_updated INTEGER NOT NULL, metadata TEXT);
    CREATE TABLE IF NOT EXISTS rooms (
      id INTEGER PRIMARY KEY AUTOINCREMENT, project_id INTEGER NOT NULL, room_id TEXT NOT NULL,
      room_name TEXT, room_number TEXT, department TEXT, level TEXT, area REAL, perimeter REAL,
      occupancy TEXT, comments TEXT, timestamp INTEGER NOT NULL, metadata TEXT,
      FOREIGN KEY(project_id) REFERENCES projects(id) ON DELETE CASCADE, UNIQUE(project_id, room_id));
    CREATE INDEX IF NOT EXISTS idx_rooms_project_id ON rooms(project_id);`);
  return native;
}
export const db = {
  prepare(sql: string) { return getDb().prepare(sql); },
  transaction<T, R>(work: (args: T) => R): (args: T) => R {
    return args => {
      const conn = getDb(); conn.exec('BEGIN IMMEDIATE');
      try { const result = work(args); conn.exec('COMMIT'); return result; }
      catch (error) { conn.exec('ROLLBACK'); throw error; }
    };
  }
};
export function closeDatabase(): void { native?.close(); native = undefined; }
export default db;
