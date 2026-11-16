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
    }
}