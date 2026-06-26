# SQL Server image for Railway. Two problems the stock image has on Railway's
# persistent volume are fixed by the entrypoint (see entrypoint.sh):
#   1. the volume is mounted root-owned, but SQL runs as non-root 'mssql'
#   2. the volume filesystem causes "misaligned log IOs" -> fatal Stack Overflow
# The entrypoint fixes ownership and forces write-through, then starts the engine,
# letting the database survive redeploys.
#
# COPY uses a repo-root-relative path because Railway builds with the repository
# root as the build context (Dockerfile Path = /backend/sqlserver/Dockerfile).
FROM mcr.microsoft.com/mssql/server:2022-latest

USER root
COPY backend/sqlserver/entrypoint.sh /usr/local/bin/entrypoint.sh
RUN chmod +x /usr/local/bin/entrypoint.sh

ENTRYPOINT ["/usr/local/bin/entrypoint.sh"]
