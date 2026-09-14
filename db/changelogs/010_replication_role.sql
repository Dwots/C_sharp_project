--liquibase formatted sql

--changeset kirill:10
CREATE ROLE replicator WITH REPLICATION LOGIN PASSWORD 'replpass';

--rollback DROP ROLE IF EXISTS replicator;
