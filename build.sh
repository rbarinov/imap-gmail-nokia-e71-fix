git pull
docker build -t e71 ./
docker rm -f e71-imap
docker rm -f e71-smtp
docker rm -f e71-pop

#secure
docker run -e LOG_LEVEL=TRACE -v /root/mail.pfx:/usr/src/app/build/mail.pfx:ro -d --restart=always --name=e71-imap -e LOCAL_PORT=143 -e HOST=imap.gmail.com -e PORT=993 -p 55143:143 e71 bash -c 'mono testimap.exe $LOCAL_PORT $HOST $PORT ./mail.pfx'
docker run -e LOG_LEVEL=TRACE -v /root/mail.pfx:/usr/src/app/build/mail.pfx:ro -d --restart=always --name=e71-smtp -e LOCAL_PORT=25 -e HOST=smtp.gmail.com -e PORT=465 -p 55025:25 e71 bash -c 'mono testimap.exe $LOCAL_PORT $HOST $PORT ./mail.pfx'
docker run -e LOG_LEVEL=TRACE -v /root/mail.pfx:/usr/src/app/build/mail.pfx:ro -d --restart=always --name=e71-pop -e LOCAL_PORT=110 -e HOST=pop.gmail.com -e PORT=995 -p 55110:110 e71 bash -c 'mono testimap.exe $LOCAL_PORT $HOST $PORT ./mail.pfx'
