#!/usr/bin/env sh
set -eu

certificate_directory=${1:-tests/ldap/certs}
mkdir -p "$certificate_directory"
openssl req -x509 -nodes -newkey rsa:2048 \
  -keyout "$certificate_directory/server.key" \
  -out "$certificate_directory/server.crt" \
  -days 2 -sha256 \
  -subj "/CN=localhost" \
  -addext "subjectAltName=DNS:localhost" \
  -addext "basicConstraints=critical,CA:TRUE" \
  -addext "keyUsage=critical,digitalSignature,keyEncipherment,keyCertSign"

