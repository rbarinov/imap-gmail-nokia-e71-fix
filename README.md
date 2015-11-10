Generate key

```sh
openssl genrsa -des3 -out ca.key 2048
openssl req -x509 -new -nodes -key ca.key -days 1024 -out ca.pem

openssl genrsa -out mail.key 2048
openssl req -new -key mail.key -out mail.csr
openssl x509 -req -in mail.csr -CA ca.pem -CAkey ca.key -CAcreateserial -out mail.crt -days 500

openssl pkcs12 -export -out mail.pfx -inkey mail.key -in mail.crt -certfile ca.pem
```

