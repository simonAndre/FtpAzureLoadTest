# FtpAzureLoadTest


docker build -t ftpazuretest .

docker run -it -e FtpAzureLoadTest__pass=****** -v outdata:/outdata  ftpazuretest