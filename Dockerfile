FROM mono:onbuild
EXPOSE 8143

WORKDIR /usr/src/app/build/

CMD mono /usr/src/app/build/testimap.exe $LOCAL_PORT $HOST $PORT
