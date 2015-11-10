Generate key

```sh

openssl genrsa -des3 -out ca.key 2048
openssl req -x509 -new -nodes -key ca.key -days 1024 -out ca.pem

openssl genrsa -out mail.key 2048
openssl req -new -key mail.key -out mail.csr
openssl x509 -req -in mail.csr -CA ca.pem -CAkey ca.key -CAcreateserial -out mail.crt -days 500

openssl pkcs12 -export -out mail.pfx -inkey mail.key -in mail.crt -certfile ca.pem

```

While running put mail.pfx file into /usr/src/app/build/ of docker container

```sh

docker rm -f e71-imap
docker rm -f e71-smtp

#secure
docker run -v /root/mail.pfx:/usr/src/app/build/mail.pfx:ro -d --restart=always --name=e71-imap -e LOCAL_PORT=143 -e HOST=imap.gmail.com -e PORT=993 -p 55143:143 e71 bash -c 'mono testimap.exe $LOCAL_PORT $HOST $PORT ./mail.pfx'
docker run -v /root/mail.pfx:/usr/src/app/build/mail.pfx:ro -d --restart=always --name=e71-smtp -e LOCAL_PORT=25 -e HOST=smtp.gmail.com -e PORT=465 -p 55025:25 e71 bash -c 'mono testimap.exe $LOCAL_PORT $HOST $PORT ./mail.pfx'


```