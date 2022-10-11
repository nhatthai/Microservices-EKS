# Microservice run on EKS
Sample Microservice on EKS, using Github Actions for deployment to EKS

## Goals
+ Using Terraform: create Infastructure in EKS
+ Deploy microservice on AWS EKS with Github Actions
+ Message Bus with RabbitMQ and SQS
+ Integrate OpenTelemetry for tracing and metrics between services
    - Using TraceContextPropagator
+ Integrate with Auth0
+ Sync the log files to New Relic for troubleshooting and debugging


### Usage
+ Update kubeconfig
    ```
    aws eks update-kubeconfig --region ap-southeast-1 --name microservice-eks-b3amei5h
    ```

+ Create Service Account
    ```
    eksctl create iamserviceaccount --cluster=microservice-eks-WHlr157J --name=aws-load-balancer-controller --namespace=kube-system --role-name eksctl-eks-github-addon-iamserviceaccount-ku-Role1 --attach-policy-arn=arn:aws:iam::783560535431:policy/ALBIngressControllerIAMPolicy --override-existing-serviceaccounts --approve

    kubectl apply -f aws/aws-load-balancer-controller-service-account.yml
    ```

+ Get IAM Service Account
    ```
    eksctl get iamserviceaccount --cluster microservice-eks-WHlr157J

    kubectl describe sa aws-load-balancer-controller -n kube-system

    kubectl apply -k "github.com/aws/eks-charts/stable/aws-load-balancer-controller/crds?ref=master"

    helm install aws-load-balancer-controller eks/aws-load-balancer-controller --set clusterName=microservice-eks-b3amei5h --set serviceAccount.create=false --set serviceAccount.name=aws-load-balancer-controller -n kube-system
    ```

+ Verify the AWS Load Balancer Controller
    ```
    kubectl get deployment -n kube-system aws-load-balancer-controller
    ```

+ Get logging AWS Load Balancer Controller


### References
+ [Using W3C Trace Context standard in distributed tracing](https://dev.to/luizhlelis/c-using-w3c-trace-context-standard-in-distributed-tracing-1nm0)
+ [Create an ALB Ingress in Amazon EKS](https://aws.amazon.com/premiumsupport/knowledge-center/eks-alb-ingress-aws-waf/)
+ [AWS Load Balancer Controller](https://docs.aws.amazon.com/eks/latest/userguide/aws-load-balancer-controller.html)