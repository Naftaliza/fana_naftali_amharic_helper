#!/bin/bash
set -euo pipefail

# 1) Permissions: Railway mounts the volume at /var/opt/mssql owned by root, but SQL
#    Server runs as the non-root 'mssql' user (uid 10001) and must own its data dir,
#    or it crashes writing /var/opt/mssql/.system ("Access is denied").
mkdir -p /var/opt/mssql
chown -R mssql:root /var/opt/mssql
chmod -R 0775 /var/opt/mssql

# 2) IO compatibility: Railway's volume filesystem doesn't honor the write
#    alignment/ordering SQL Server's log writer expects, which triggers
#    "misaligned log IOs ... falling back to synchronous IO" and a fatal Stack
#    Overflow crash on boot. Forcing write-through makes SQL use a volume-safe IO path.
cat > /var/opt/mssql/mssql.conf <<'CONF'
[control]
writethrough = 1
alternatewritethrough = 1
CONF
chown mssql:root /var/opt/mssql/mssql.conf

# Drop privileges and start the engine.
exec runuser -u mssql -- /opt/mssql/bin/sqlservr
