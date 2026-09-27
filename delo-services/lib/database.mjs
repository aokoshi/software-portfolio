import { DatabaseSync } from 'node:sqlite';
import { mkdirSync } from 'node:fs';
import { dirname } from 'node:path';

export const categories = { design: 'Дизайн', development: 'Разработка', consulting: 'Консультации', marketing: 'Маркетинг', text: 'Тексты' };
export const logos = [
  { id: 'design', symbol: '✦', name: 'Дизайн', color: 'violet' },
  { id: 'code', symbol: '⌘', name: 'Разработка', color: 'blue' },
  { id: 'slides', symbol: '▤', name: 'Презентации', color: 'orange' },
  { id: 'bot', symbol: '⚙', name: 'Автоматизация', color: 'green' },
  { id: 'idea', symbol: '☀', name: 'Идеи', color: 'yellow' },
  { id: 'text', symbol: '¶', name: 'Тексты', color: 'pink' },
  { id: 'growth', symbol: '↗', name: 'Развитие', color: 'blue' },
  { id: 'research', symbol: '◎', name: 'Исследования', color: 'green' }
];

export function openDatabase(path) {
  if (path !== ':memory:') mkdirSync(dirname(path), { recursive: true });
  const db = new DatabaseSync(path);
  db.exec(`
    PRAGMA journal_mode = WAL;
    PRAGMA foreign_keys = ON;
    PRAGMA busy_timeout = 5000;
    CREATE TABLE IF NOT EXISTS users (
      id TEXT PRIMARY KEY, name TEXT NOT NULL, email TEXT NOT NULL UNIQUE,
      password_hash TEXT NOT NULL, role TEXT NOT NULL CHECK(role IN ('client','seller')),
      created_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now'))
    ) STRICT;
    CREATE TABLE IF NOT EXISTS sessions (
      token_hash TEXT PRIMARY KEY, user_id TEXT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
      expires_at INTEGER NOT NULL
    ) STRICT;
    CREATE TABLE IF NOT EXISTS services (
      id TEXT PRIMARY KEY, seller_id TEXT NOT NULL REFERENCES users(id), title TEXT NOT NULL,
      description TEXT NOT NULL, category TEXT NOT NULL, logo TEXT NOT NULL,
      price INTEGER NOT NULL CHECK(price > 0), days INTEGER NOT NULL CHECK(days BETWEEN 1 AND 365),
      active INTEGER NOT NULL DEFAULT 1 CHECK(active IN (0,1)),
      created_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now'))
    ) STRICT;
    CREATE TABLE IF NOT EXISTS conversations (
      id TEXT PRIMARY KEY, service_id TEXT NOT NULL REFERENCES services(id),
      client_id TEXT NOT NULL REFERENCES users(id), seller_id TEXT NOT NULL REFERENCES users(id),
      created_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
      UNIQUE(service_id,client_id)
    ) STRICT;
    CREATE TABLE IF NOT EXISTS orders (
      id TEXT PRIMARY KEY, service_id TEXT NOT NULL REFERENCES services(id),
      client_id TEXT NOT NULL REFERENCES users(id), seller_id TEXT NOT NULL REFERENCES users(id),
      conversation_id TEXT NOT NULL REFERENCES conversations(id), title TEXT NOT NULL,
      price INTEGER NOT NULL, days INTEGER NOT NULL, brief TEXT NOT NULL,
      status TEXT NOT NULL DEFAULT 'new' CHECK(status IN ('new','in_progress','delivered','completed','cancelled')),
      request_key TEXT NOT NULL, created_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
      UNIQUE(client_id,request_key)
    ) STRICT;
    CREATE TABLE IF NOT EXISTS messages (
      id INTEGER PRIMARY KEY AUTOINCREMENT, conversation_id TEXT NOT NULL REFERENCES conversations(id),
      sender_id TEXT NOT NULL REFERENCES users(id), body TEXT NOT NULL,
      created_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now'))
    ) STRICT;
    CREATE TABLE IF NOT EXISTS conversation_reads (
      conversation_id TEXT NOT NULL REFERENCES conversations(id), user_id TEXT NOT NULL REFERENCES users(id),
      last_message_id INTEGER NOT NULL DEFAULT 0, PRIMARY KEY(conversation_id,user_id)
    ) STRICT;
    CREATE INDEX IF NOT EXISTS sessions_expiry ON sessions(expires_at);
    CREATE INDEX IF NOT EXISTS services_seller ON services(seller_id,active);
    CREATE INDEX IF NOT EXISTS orders_client ON orders(client_id,created_at);
    CREATE INDEX IF NOT EXISTS orders_seller ON orders(seller_id,created_at);
    CREATE INDEX IF NOT EXISTS conversations_client ON conversations(client_id);
    CREATE INDEX IF NOT EXISTS conversations_seller ON conversations(seller_id);
    CREATE INDEX IF NOT EXISTS messages_conversation ON messages(conversation_id,id);
    PRAGMA user_version = 1;
  `);
  return db;
}

// Transactions are synchronous: no await may occur within this callback.
export function transaction(db, fn) {
  db.exec('BEGIN IMMEDIATE');
  try { const result = fn(); db.exec('COMMIT'); return result; }
  catch (error) { db.exec('ROLLBACK'); throw error; }
}
