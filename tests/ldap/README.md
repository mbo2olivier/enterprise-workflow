# Qualification OpenLDAP L5a

Cette stack démarre un OpenLDAP 2.6 non AD à partir de l’image multiarchitecture `vegardit/openldap`, épinglée par digest. Les données sont éphémères et réservées aux tests. Le mot de passe visible dans la composition n’est donc pas un secret de production.

```sh
./tests/ldap/generate-test-certificate.sh
docker compose -f tests/ldap/docker-compose.yml up -d --wait
LDAP_TEST_ENABLED=1 LDAP_TEST_LDAPS=1 LDAP_TEST_CA_FILE="$PWD/tests/ldap/certs/ca.crt" \
  LDAPTLS_CACERT="$PWD/tests/ldap/certs/ca.crt" LDAPTLS_REQCERT=demand \
  dotnet test tests/EnterpriseWorkflow.Security.Tests/EnterpriseWorkflow.Security.Tests.csproj --configuration Release
docker compose -f tests/ldap/docker-compose.yml down --volumes
```

Le certificat de test n’est pas versionné. Il porte uniquement `localhost`, ce qui permet aussi de vérifier qu’une connexion à `127.0.0.1` échoue sur le contrôle du nom.
