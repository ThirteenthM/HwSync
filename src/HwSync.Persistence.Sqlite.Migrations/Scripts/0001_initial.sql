CREATE TABLE participant (
    singleton INTEGER PRIMARY KEY CHECK (singleton = 1),
    participant_id TEXT NOT NULL UNIQUE
);
INSERT INTO participant(singleton, participant_id) VALUES (1, lower(hex(randomblob(16))));

CREATE TABLE folders (
    folder_id TEXT PRIMARY KEY,
    root_path TEXT NOT NULL COLLATE NOCASE UNIQUE
);
CREATE TABLE file_snapshots (
    folder_id TEXT NOT NULL REFERENCES folders(folder_id),
    relative_path TEXT NOT NULL COLLATE NOCASE,
    size INTEGER NOT NULL CHECK (size >= 0),
    modified_utc TEXT NOT NULL,
    PRIMARY KEY (folder_id, relative_path)
);
CREATE TABLE deletion_events (
    event_number INTEGER PRIMARY KEY AUTOINCREMENT,
    folder_id TEXT NOT NULL REFERENCES folders(folder_id),
    origin_participant_id TEXT NOT NULL,
    relative_path TEXT NOT NULL COLLATE NOCASE,
    deleted_utc TEXT NOT NULL,
    previous_size INTEGER NOT NULL CHECK (previous_size >= 0),
    previous_modified_utc TEXT NOT NULL,
    active INTEGER NOT NULL CHECK (active IN (0, 1))
);
CREATE INDEX deletion_events_folder ON deletion_events(folder_id, event_number);

CREATE TABLE acknowledged_states (
    client_id TEXT NOT NULL,
    folder_id TEXT NOT NULL,
    state_json TEXT NOT NULL,
    PRIMARY KEY(client_id, folder_id)
);
