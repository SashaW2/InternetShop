@echo off
set JENKINS_WAR=E:\Jenkins\jenkins.war
set HTTP_PORT=8080
java -jar "%JENKINS_WAR%" --httpPort=%HTTP_PORT% --enable-future-java
pause