# SQL Server image for Railway. Railway mounts the persistent volume at
# /var/opt/mssql owned by root; the stock image runs as the non-root 'mssql' user
# and cannot write there, so it crashes on boot. This image adds a root entrypoint
# that fixes ownership of the mounted volume at runtime, then drops to 'mssql' to
# start the engine — letting the database survive redeploys.
FROM mcr.microsoft.com/mssql/server:2022-latest

USER root
COPY entrypoint.sh /usr/local/bin/entrypoint.sh
RUN chmod +x /usr/local/bin/entrypoint.sh

ENTRYPOINT ["/usr/local/bin/entrypoint.sh"]
