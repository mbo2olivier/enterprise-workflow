#!/usr/bin/env sh
set -eu

certificate_directory=${1:-tests/ldap/certs}
mkdir -p "$certificate_directory"

openssl req -x509 -nodes -newkey rsa:2048 \
  -keyout "$certificate_directory/ca.key" \
  -out "$certificate_directory/ca.crt" \
  -days 2 -sha256 \
  -subj "/CN=Enterprise Workflow LDAP Test CA" \
  -addext "basicConstraints=critical,CA:TRUE" \
  -addext "keyUsage=critical,keyCertSign,cRLSign"

openssl req -nodes -newkey rsa:2048 \
  -keyout "$certificate_directory/server.key" \
  -out "$certificate_directory/server.csr" \
  -subj "/CN=localhost"

openssl x509 -req \
  -in "$certificate_directory/server.csr" \
  -CA "$certificate_directory/ca.crt" \
  -CAkey "$certificate_directory/ca.key" \
  -CAcreateserial \
  -out "$certificate_directory/server.crt" \
  -days 2 -sha256 \
  -extfile "$(dirname "$0")/server-cert.ext"

mkdir -p "$certificate_directory/trust"
cp "$certificate_directory/ca.crt" "$certificate_directory/trust/enterprise-workflow-ldap-ca.crt"
openssl rehash "$certificate_directory/trust"
