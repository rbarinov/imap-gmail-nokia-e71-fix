git pull && docker build -t e71 ./ && docker rm -f e71;
docker run -d --restart=always --name=e71 -p 55143:8143 e71
