pipeline {
    agent any
    stages {
        stage('Checkout') {
            steps {
                checkout scm
            }
        }
        stage('Restore') {
            steps {
                bat 'dotnet restore InternetShop.slnx'
            }
        }
        stage('Build') {
            steps {
                bat 'dotnet build InternetShop.slnx --configuration Release --no-restore'
            }
        }
        stage('Test') {
            steps {
                bat 'dotnet test InternetShop.Tests/InternetShop.Tests.csproj --configuration Release --no-build'
            }
        }
        stage('Package') {
            steps {
                bat 'docker build -t internetshop-server:%BUILD_NUMBER% -f InternetShop.Server/Dockerfile .'
            }
        }
        stage('Deploy') {
            steps {
                bat 'kubectl set image deployment/internetshop-server server=internetshop-server:%BUILD_NUMBER%'
                bat 'kubectl rollout status deployment/internetshop-server --timeout=120s'
                bat 'kubectl get deployments'
                bat 'kubectl get pods'
            }
        }
    }
}