FROM mono:onbuild
EXPOSE 8143

WORKDIR /usr/src/app/build/
USER root
CMD mono testimap.exe $LOCAL_PORT $HOST $PORT
