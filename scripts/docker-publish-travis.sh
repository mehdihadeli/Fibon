#!/bin/bash
DOCKER_ENV=''
DOCKER_TAG=''

case "$TRAVIS_BRANCH" in
  "master")
    DOCKER_ENV=production
    DOCKER_TAG=latest
    ;;
  "develop")
    DOCKER_ENV=development
    DOCKER_TAG=dev
    ;;    
esac

docker login -u $DOCKER_USERNAME -p $DOCKER_PASSWORD

docker build -t fibon-api:$DOCKER_TAG -f ./src/Fibon.Api/Dockerfile ./src/Fibon.Api 
# docker build -t fibon-service:$DOCKER_TAG -f ./src/Fibon.Service/Dockerfile ./src/Fibon.Service 

docker tag fibon-api:$DOCKER_TAG $DOCKER_USERNAME/fibon-api:$DOCKER_TAG
# docker tag fibon-service:$DOCKER_TAG $DOCKER_USERNAME/fibon-service:$DOCKER_TAG

docker push $DOCKER_USERNAME/fibon-api:$DOCKER_TAG
# docker push $DOCKER_USERNAME/fibon-service:$DOCKER_TAG