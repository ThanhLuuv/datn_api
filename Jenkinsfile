pipeline {
  agent any

  environment {
    IMAGE        = 'bookstore/api'        // Docker image name (tên ảnh)
    TAG          = "${env.BUILD_NUMBER}"  // Tag theo số build
    DEPLOY_DIR   = '/srv/bookstore-api'   // Thư mục triển khai trên server
    COMPOSE_FILE = 'docker-compose.yml'
    HEALTH_PORT  = '3001'                 // Cổng để healthcheck
    HEALTH_PATH  = '/health'              // Đường dẫn healthcheck
    // Gemini API credentials - tên credentials trong Jenkins: 'bookstore-gemini-api-key'
    GEMINI_API_KEY = credentials('bookstore-gemini-api-key')
  }

  options { timestamps() }

  stages {
    stage('Checkout (lấy mã)') {
      steps { checkout scm }
    }

    stage('Build Docker image (đóng gói)') {
      steps {
        sh '''
          docker build -t ${IMAGE}:${TAG} -t ${IMAGE}:latest .
        '''
      }
    }

    stage('Prepare deploy dir (chuẩn bị triển khai)') {
      steps {
        sh '''
          mkdir -p ${DEPLOY_DIR}
          rsync -av ${COMPOSE_FILE} ${DEPLOY_DIR}/
        '''
      }
    }

    stage('Deploy with Compose (triển khai)') {
      steps {
        sh '''
          cd ${DEPLOY_DIR}
          export BUILD_TAG=${TAG}
          # Export biến môi trường để docker-compose có thể đọc được
          export GEMINI_API_KEY="${GEMINI_API_KEY}"
          export GEMINI_MODEL="${GEMINI_MODEL:-gemini-2.5-flash}"
          export GEMINI_BASE_URL="${GEMINI_BASE_URL:-https://generativelanguage.googleapis.com}"
          # Đảm bảo biến môi trường Gemini được truyền vào container
          # GEMINI_API_KEY được lấy từ Jenkins credentials: 'bookstore-gemini-api-key'
          if [ -z "${GEMINI_API_KEY}" ]; then
            echo "Warning: GEMINI_API_KEY is not set. Gemini features may not work."
            echo "Please configure Jenkins credential with ID: bookstore-gemini-api-key"
          else
            echo "Gemini API key configured successfully (length: ${#GEMINI_API_KEY} chars)"
          fi
          # Không pull backend (image build local); không build lại tại deploy
          # Truyền biến môi trường trực tiếp vào docker compose
          GEMINI_API_KEY="${GEMINI_API_KEY}" \
          GEMINI_MODEL="${GEMINI_MODEL:-gemini-2.5-flash}" \
          GEMINI_BASE_URL="${GEMINI_BASE_URL:-https://generativelanguage.googleapis.com}" \
          docker compose -f ${COMPOSE_FILE} up -d
          
          # Kiểm tra biến môi trường trong container
          echo "Checking environment variables in container..."
          sleep 2
          docker compose -f ${COMPOSE_FILE} exec -T backend sh -c 'env | grep -i gemini || echo "No Gemini env vars found"' || true
          
          docker image prune -f || true
        '''
      }
    }

    stage('DB migration (tuỳ chọn)') {
      when {
        expression { return params.RUN_MIGRATIONS == true }
      }
      steps {
        sh '''
          cd ${DEPLOY_DIR}
          if docker compose -f ${COMPOSE_FILE} exec -T backend sh -lc "dotnet ef --version 2>/dev/null || echo 'no-ef'"; then
            docker compose -f ${COMPOSE_FILE} exec -T backend dotnet ef database update || echo "Migration failed or not needed"
          else
            echo "No EF tools available; skipping migration"
          fi
        '''
      }
    }

    stage('Healthcheck (kiểm tra sống)') {
      steps {
        sh '''
          for i in {1..30}; do
            if curl -fsS http://103.221.223.183:${HEALTH_PORT}${HEALTH_PATH} > /dev/null; then
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
    success { echo 'Deploy OK (Docker Compose)' }
    failure { echo 'Deploy FAILED' }
  }
}
