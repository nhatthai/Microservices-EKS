# Create IAM role for service accounts (IRSA) for use within EKS clusters
module "lb_irsa_role" {
  source    = "terraform-aws-modules/iam/aws//modules/iam-role-for-service-accounts-eks"

  role_name = "aws-load-balancer-controller"
  attach_load_balancer_controller_policy = true

  oidc_providers = {
    main = {
      provider_arn               = module.eks.oidc_provider_arn
      namespace_service_accounts = ["kube-system:aws-load-balancer-controller"]
    }
  }

  tags = {
    Name = "vpc-eks-lb-irsa"
  }
}

resource "kubernetes_service_account" "service-account" {
  metadata {
    name = "aws-load-balancer-controller"
    namespace = "kube-system"
    labels = {
        "app.kubernetes.io/name"= "aws-load-balancer-controller"
        "app.kubernetes.io/component"= "controller"
    }
    annotations = {
      "eks.amazonaws.com/role-arn" = "arn:aws:iam::${local.account_id}:role/aws-load-balancer-controller"
      "eks.amazonaws.com/sts-regional-endpoints" = "true"
    }
  }
}

resource "helm_release" "alb" {
  repository = "https://aws.github.io/eks-charts"
  chart      = "aws-load-balancer-controller"
  name  	=  "aws-load-balancer-controller"
  namespace = "kube-system"

  depends_on = [
    kubernetes_service_account.service-account
  ]

  set {
    name  = "region"
    value = var.region
  }

  set {
    name  = "vpcId"
    value = module.vpc.vpc_id
  }

  set {
    name  = "image.repository"
    value = "602401143452.dkr.ecr.eu-west-2.amazonaws.com/amazon/aws-load-balancer-controller"
  }

  set {
    name  = "serviceAccount.create"
    value = "false"
  }

  set {
    name  = "serviceAccount.name"
    value = "aws-load-balancer-controller"
  }

  set {
    name  = "clusterName"
    value = local.cluster_name
  }
}
