FROM stilliard/pure-ftpd

# e.g. you could change the defult command run:
CMD /run.sh -c 30 -C 10 -l puredb:/etc/pure-ftpd/pureftpd.pdb -E -j -R -P $PUBLICHOST -p 30000:30059

# docker run -d --name ftpserver -p 21:21 -p 30000-30009:30000-30009 -e "PUBLICHOST=localhost" -e FTP_USER_NAME=bob -e FTP_USER_PASS=12345 -e FTP_USER_HOME=/home/bob stilliard/pure-ftpd