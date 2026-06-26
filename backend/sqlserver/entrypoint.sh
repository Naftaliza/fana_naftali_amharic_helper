#!/bin/bash
set -euo pipefail

# Railway mounts the persistent volume at /var/opt/mssql owned by root, but SQL
# Server runs as the non-root 'mssql' user (uid 10001) and must own its data
# directory — otherwise it crashes on boot trying to write /var/opt/mssql/.system
# ("Access is denied"). We run this entrypoint as root, hand the directory to
# mssql, then drop privileges to start the engine.
mkdir -p /var/opt/mssql
chown -R mssql:root /var/opt/mssql
chmod -R 0775 /var/opt/mssql

exec runuser -u mssql -- /opt/mssql/bin/sqlservr
