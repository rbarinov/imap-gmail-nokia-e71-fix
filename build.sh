git pull
docker build -t e71 ./
docker rm -f e71-imap
docker rm -f e71-smtp

docker run -d --restart=always --name=e71-imap -e LOCAL_PORT=143 -e HOST=imap.gmail.com -e PORT=993 -p 55143:143 e71
docker run -d --restart=always --name=e71-smtp -e LOCAL_PORT=25 -e HOST=smtp.gmail.com -e PORT=465 -p 55025:25 e71
