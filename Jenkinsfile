pipeline {
  agent any

  environment {
    IMAGE        = 'bookstore/api'        // Docker image name for backend
    TAG          = "${env.BUILD_NUMBER}"  // Tag by build number
    DEPLOY_DIR   = '/srv/bookstore-api'   // Deploy directory on server
    COMPOSE_FILE = 'docker-compose.yml'   // Compose file in this repo

    // Healthcheck config
    HEALTH_HOST  = '127.0.0.1'            // Server itself
    HEALTH_PORT  = '3001'                 // Đổi sang 3001
    HEALTH_PATH  = '/health'              // Nếu API bạn có endpoint /health thì giữ, không thì chỉnh lại
  }

  options { timestamps() }

  stages {
    stage('Checkout') {
      steps { checkout scm }
    }

    stage('Build Docker image') {
      steps {
        sh '''
          docker build \
            -f Dockerfile \
            -t ${IMAGE}:${TAG} \
            -t ${IMAGE}:latest \
            .
        '''
      }
    }

    stage('Prepare deploy dir') {
      steps {
        sh '''
          mkdir -p ${DEPLOY_DIR}
          rsync -av ${COMPOSE_FILE} ${DEPLOY_DIR}/
        '''
      }
    }

    stage('Deploy (Docker Compose)') {
      steps {
        sh '''
          cd ${DEPLOY_DIR}
          export BUILD_TAG=${TAG} IMAGE=${IMAGE} HEALTH_PATH=${HEALTH_PATH}
          docker compose -f ${COMPOSE_FILE} up -d --remove-orphans
          docker image prune -f || true
        '''
      }
    }

    stage('Healthcheck') {
      steps {
        sh '''
          for i in {1..30}; do
            if curl -fsS "http://${HEALTH_HOST}:${HEALTH_PORT}${HEALTH_PATH}" > /dev/null; then
              echo "Healthy on :${HEALTH_PORT}"; exit 0
            fi
            sleep 2
          done
          echo "Healthcheck failed"
          docker compose -f ${DEPLOY_DIR}/${COMPOSE_FILE} logs --no-color backend || true
          exit 1
        '''
      }
    }
  }

  post {
    success { echo 'Deploy BE OK (Docker Compose)' }
    failure { echo 'Deploy BE FAILED' }
  }
}
