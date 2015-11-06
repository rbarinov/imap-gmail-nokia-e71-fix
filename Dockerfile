FROM mono:onbuild
EXPOSE 8143

CMD mono /usr/src/app/build/testimap.exe

