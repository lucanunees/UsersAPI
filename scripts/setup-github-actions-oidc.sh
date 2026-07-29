#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 1 ]]; then
  echo "Uso: $0 owner/repo [role-name]"
  exit 1
fi

REPO_FULL_NAME="$1"
ROLE_NAME="${2:-GitHubActionsDeployRoleUsersAPI}"
AWS_REGION="${AWS_REGION:-us-east-1}"
EKS_CLUSTER_NAME="${EKS_CLUSTER_NAME:-fiap-games-eks}"
K8S_NAMESPACE="${K8S_NAMESPACE:-fcg-tech-fase-4}"

if ! command -v aws >/dev/null 2>&1; then
  echo "Erro: AWS CLI não encontrado."
  exit 1
fi

ACCOUNT_ID="$(aws sts get-caller-identity --query Account --output text)"
PROVIDER_URL="token.actions.githubusercontent.com"
PROVIDER_ARN="arn:aws:iam::${ACCOUNT_ID}:oidc-provider/${PROVIDER_URL}"

if ! aws iam get-open-id-connect-provider --open-id-connect-provider-arn "$PROVIDER_ARN" >/dev/null 2>&1; then
  echo "Criando provider OIDC para GitHub Actions..."
  aws iam create-open-id-connect-provider \
    --url "https://${PROVIDER_URL}" \
    --client-id-list sts.amazonaws.com \
    --thumbprint-list 6938fd4d98bab03faadb97b34396831e3780aea1 >/dev/null
else
  echo "Provider OIDC já existe: ${PROVIDER_ARN}"
fi

TRUST_POLICY_FILE="$(mktemp)"
PERMISSIONS_POLICY_FILE="$(mktemp)"

cat > "$TRUST_POLICY_FILE" <<EOF
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Principal": {
        "Federated": "${PROVIDER_ARN}"
      },
      "Action": "sts:AssumeRoleWithWebIdentity",
      "Condition": {
        "StringEquals": {
          "token.actions.githubusercontent.com:aud": "sts.amazonaws.com"
        },
        "StringLike": {
          "token.actions.githubusercontent.com:sub": [
            "repo:${REPO_FULL_NAME}:ref:refs/heads/develop",
            "repo:${REPO_FULL_NAME}:environment:develop",
            "repo:${REPO_FULL_NAME}:environment:prod",
            "repo:${REPO_FULL_NAME}:ref:refs/tags/v*"
          ]
        }
      }
    }
  ]
}
EOF

cat > "$PERMISSIONS_POLICY_FILE" <<EOF
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "EcrAuth",
      "Effect": "Allow",
      "Action": [
        "ecr:GetAuthorizationToken"
      ],
      "Resource": "*"
    },
    {
      "Sid": "EcrPushPullUsersApi",
      "Effect": "Allow",
      "Action": [
        "ecr:BatchCheckLayerAvailability",
        "ecr:BatchGetImage",
        "ecr:CompleteLayerUpload",
        "ecr:GetDownloadUrlForLayer",
        "ecr:InitiateLayerUpload",
        "ecr:PutImage",
        "ecr:UploadLayerPart"
      ],
      "Resource": "arn:aws:ecr:${AWS_REGION}:${ACCOUNT_ID}:repository/usersapi"
    },
    {
      "Sid": "EksClusterAccess",
      "Effect": "Allow",
      "Action": [
        "eks:DescribeCluster",
        "eks:CreateAccessEntry",
        "eks:DescribeAccessEntry",
        "eks:ListAssociatedAccessPolicies",
        "eks:AssociateAccessPolicy"
      ],
      "Resource": "arn:aws:eks:${AWS_REGION}:${ACCOUNT_ID}:cluster/${EKS_CLUSTER_NAME}"
    }
  ]
}
EOF

if aws iam get-role --role-name "$ROLE_NAME" >/dev/null 2>&1; then
  echo "Atualizando trust policy da role ${ROLE_NAME}..."
  aws iam update-assume-role-policy \
    --role-name "$ROLE_NAME" \
    --policy-document "file://${TRUST_POLICY_FILE}" >/dev/null
else
  echo "Criando role ${ROLE_NAME}..."
  aws iam create-role \
    --role-name "$ROLE_NAME" \
    --assume-role-policy-document "file://${TRUST_POLICY_FILE}" >/dev/null
fi

echo "Aplicando policy de permissões na role ${ROLE_NAME}..."
aws iam put-role-policy \
  --role-name "$ROLE_NAME" \
  --policy-name "${ROLE_NAME}-InlinePolicy" \
  --policy-document "file://${PERMISSIONS_POLICY_FILE}" >/dev/null

ROLE_ARN="arn:aws:iam::${ACCOUNT_ID}:role/${ROLE_NAME}"

echo ""
echo "Role configurada com sucesso."
echo "Role ARN: ${ROLE_ARN}"
echo ""
echo "Use estes valores no GitHub (repo ${REPO_FULL_NAME}):"
echo "- Secret AWS_ROLE_TO_ASSUME=${ROLE_ARN}"
echo "- Variable EKS_CLUSTER_NAME=${EKS_CLUSTER_NAME}"
echo "- Variable K8S_NAMESPACE=${K8S_NAMESPACE}"

rm -f "$TRUST_POLICY_FILE" "$PERMISSIONS_POLICY_FILE"
