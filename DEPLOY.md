# StructFlow AWS 배포 가이드

EC2 t3.micro(무료 티어) + ECR + GitHub Actions CI/CD 구성입니다.

---

## 아키텍처

```
GitHub Actions
    │ push to main
    ▼
ECR (Docker 이미지 저장소)
    │
    ▼
EC2 t3.micro (Amazon Linux 2023)
    ├── simulation-api  :5000  ← .NET 10 API
    ├── n8n             :5678  ← 워크플로우
    └── nginx           :80   ← 리버스 프록시 (외부 공개)
```

**공개 URL:**
- `http://<EC2-IP>/api/simulate` — SimulationEngine
- `http://<EC2-IP>/webhook/structflow` — n8n Webhook
- `http://<EC2-IP>/n8n/` — n8n 관리 UI

---

## 1단계: ECR 레포지토리 생성

```bash
# AWS CLI 설치 및 configure 완료 후
aws ecr create-repository \
  --repository-name structflow-api \
  --region ap-northeast-2

# 출력된 repositoryUri를 기록해 두세요
# 예: 123456789.dkr.ecr.ap-northeast-2.amazonaws.com/structflow-api
```

---

## 2단계: EC2 인스턴스 생성

AWS 콘솔 → EC2 → Launch Instance:

| 설정 | 값 |
|------|-----|
| AMI | Amazon Linux 2023 |
| 인스턴스 타입 | t3.micro (프리 티어) |
| 스토리지 | 20 GB gp3 |
| 보안 그룹 인바운드 | SSH (22), HTTP (80) |
| 키 페어 | 새 키 페어 생성 → `.pem` 저장 |

---

## 3단계: EC2 초기 설정

```bash
# EC2에 SSH 접속
ssh -i your-key.pem ec2-user@<EC2-PUBLIC-IP>

# Docker 설치
sudo yum update -y
sudo yum install -y docker
sudo systemctl start docker
sudo systemctl enable docker
sudo usermod -aG docker ec2-user
newgrp docker

# Docker Compose 설치
sudo curl -L "https://github.com/docker/compose/releases/latest/download/docker-compose-$(uname -s)-$(uname -m)" \
  -o /usr/local/bin/docker-compose
sudo chmod +x /usr/local/bin/docker-compose

# AWS CLI (EC2 AMI에 기본 포함. 없으면 설치)
aws configure
# AWS_ACCESS_KEY_ID, AWS_SECRET_ACCESS_KEY, 리전(ap-northeast-2) 입력

# 배포 디렉토리 생성
mkdir -p ~/structflow
```

---

## 4단계: 환경 변수 설정

```bash
cd ~/structflow
cat > .env << 'EOF'
N8N_HOST=<EC2_PUBLIC_IP>
N8N_PROTOCOL=http
N8N_WEBHOOK_URL=http://<EC2_PUBLIC_IP>/webhook/
N8N_ENCRYPTION_KEY=$(openssl rand -hex 32)
N8N_BASIC_AUTH_USER=admin
N8N_BASIC_AUTH_PASSWORD=<강력한비밀번호>
EOF
```

---

## 5단계: GitHub Secrets 등록

GitHub 저장소 → Settings → Secrets → Actions → New repository secret:

| Secret 이름 | 값 |
|------------|-----|
| `AWS_ACCESS_KEY_ID` | IAM 액세스 키 |
| `AWS_SECRET_ACCESS_KEY` | IAM 시크릿 키 |
| `AWS_REGION` | `ap-northeast-2` |
| `ECR_REGISTRY` | `123456789.dkr.ecr.ap-northeast-2.amazonaws.com` |
| `EC2_HOST` | EC2 퍼블릭 IP |
| `EC2_USER` | `ec2-user` |
| `EC2_SSH_KEY` | `.pem` 파일 내용 전체 (`-----BEGIN...` 포함) |

**IAM 권한** (최소 권한 원칙):
```json
{
  "Version": "2012-10-17",
  "Statement": [
    { "Effect": "Allow", "Action": ["ecr:*"], "Resource": "*" },
    { "Effect": "Allow", "Action": ["ec2:DescribeInstances"], "Resource": "*" }
  ]
}
```
EC2에는 **AmazonEC2ContainerRegistryReadOnly** IAM 역할 연결 필요.

---

## 6단계: 첫 배포 (수동)

```bash
# 로컬에서 첫 이미지 빌드 & ECR 푸시
aws ecr get-login-password --region ap-northeast-2 | \
  docker login --username AWS --password-stdin \
  123456789.dkr.ecr.ap-northeast-2.amazonaws.com

docker build -t structflow-api:latest .
docker tag structflow-api:latest \
  123456789.dkr.ecr.ap-northeast-2.amazonaws.com/structflow-api:latest
docker push 123456789.dkr.ecr.ap-northeast-2.amazonaws.com/structflow-api:latest

# EC2에서 첫 실행
scp -i your-key.pem deploy/docker-compose.yml deploy/nginx.conf \
  ec2-user@<EC2-IP>:~/structflow/

ssh -i your-key.pem ec2-user@<EC2-IP>
cd ~/structflow
export SIMULATION_IMAGE=123456789.dkr.ecr.ap-northeast-2.amazonaws.com/structflow-api:latest
docker-compose up -d
```

---

## 7단계: CI/CD 자동화 확인

이후 `main` 브랜치에 push하면 GitHub Actions가:
1. xUnit 테스트 실행 (66건)
2. Docker 이미지 빌드 & ECR push
3. EC2 SSH → docker-compose rolling update

---

## 8단계: n8n 워크플로우 재설정

1. `http://<EC2-IP>/n8n/` 접속 (admin / 설정한 비밀번호)
2. `n8n/workflow_structflow.json` 가져오기
3. Claude API 자격증명 설정
4. SimulationEngine URL 변경: `http://localhost:5000` → `http://simulation-api:5000`
   (같은 Docker 네트워크 내 서비스명으로 접근)
5. Workflow Publish

---

## E2E 테스트

```bash
curl -X POST http://<EC2-IP>/webhook/structflow \
  -H "Content-Type: application/json" \
  -d '{"input": "직경 60cm 콘크리트 하수관, 경사 1%, 토피고 2m"}'
```

**예상 응답:**
```json
{
  "success": true,
  "result": {
    "pipe_id": "PIPE-001",
    "overall_status": "NORMAL",
    "flow": { "velocity_ms": 1.49, "fill_ratio": 0.75 },
    "stress": { "safety_factor": 2.43 }
  }
}
```

---

## 비용 예상 (AWS 프리 티어 기준)

| 리소스 | 사양 | 비용 |
|--------|------|------|
| EC2 t3.micro | 2 vCPU / 1GB RAM | **무료** (12개월) |
| ECR | 500MB/월 | **무료** |
| 데이터 전송 | 15GB/월 아웃바운드 | **무료** |
| Claude API | claude-haiku-4-5 | ~$0.001/요청 |

> 프리 티어 종료 후 t3.micro 약 **$8/월**.
