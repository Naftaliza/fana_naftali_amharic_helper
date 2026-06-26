# SQL Server image for Railway. Railway mounts the persistent volume at
# /var/opt/mssql owned by root; the stock image runs as the non-root 'mssql' user
# and cannot write there, so it crashes on boot. This image adds a root entrypoint
# that fixes ownership of the mounted volume at runtime, then drops to 'mssql' to
# start the engine — letting the database survive redeploys.
#
# The entrypoint is generated inline (no COPY) so the build works regardless of the
# build context / root directory configured in Railway.
FROM mcr.microsoft.com/mssql/server:2022-latest

USER root
RUN printf '%s\n' \
  '#!/bin/bash' \
  'set -euo pipefail' \
  '# Railway mounts the volume as root; SQL Server runs as the non-root mssql user' \
  '# and must own its data dir or it crashes writing /var/opt/mssql/.system.' \
  'mkdir -p /var/opt/mssql' \
  'chown -R mssql:root /var/opt/mssql' \
  'chmod -R 0775 /var/opt/mssql' \
  'exec runuser -u mssql -- /opt/mssql/bin/sqlservr' \
  > /usr/local/bin/entrypoint.sh \
  && chmod +x /usr/local/bin/entrypoint.sh

ENTRYPOINT ["/usr/local/bin/entrypoint.sh"]
