<div align="center">
  <a href="https://blockfrost.io/" target="_blank">    
    <img src="https://avatars.githubusercontent.com/u/9141961?s=200&v=4" width="190" alt="BlockFrost Logo" />
  </a>
</div>
<div align="center"> 
  <a href="https://dotnet.microsoft.com/apps/aspnet" target="_blank">
    <img src="https://img.shields.io/badge/.NET Platform-8.0-blue" alt=".NET Framework Version">
  </a>  
</div>

### English Version

# Backend Module for Blockchain Voting System  
**Actors**
* David Tacuri

### Summary  

The backend module for the Blockchain Voting System performs the following tasks:
1. Exposes APIs to upload scanned scrutiny records (actas de escrutinio).
2. Sends records to IPFS via REST API.
3. Generates and exports validators for Cardano smart contracts using Aiken.
4. Integrates scanning flow with Plutus script for on-chain validation.
5. Provides diagnostics to validate data format before submitting.

### Key Features

- **API Interface**: For uploading and processing scrutiny records.
- **IPFS Integration**: Sends images and data to IPFS.
- **Validator Generation**: Uses Aiken to compile and export validators.
- **On-Chain Validation**: Uses Plutus validators to perform secure checks.
- **Diagnosis Tool**: Verifies the datum integrity and structure.

### Technical Aspects

- **Programming Language**: C#
- **Framework**: .NET 8.0
- **Architecture**: RESTful APIs

### Endpoints Overview

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | /api/Acta/{id} | Retrieve acta data |
| POST | /api/Acta/{id}/escaneo | Send acta to backend |
| POST | /api/Acta/validadores/exportar | Export Plutus validators |
| POST | /api/Acta/{id}/escaneo-plutus | Upload and validate using Plutus |
| POST | /api/Acta/diagnose-datum | Diagnose datum structure |

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- Visual Studio 2022 or later

### Installation Procedure

1. Clone the repository:  
```bash
git clone https://github.com/democracyonchain/asuncion-backend-onchain.git
```
2. Open the solution in Visual Studio.
3. Restore NuGet packages.
4. Build the project.
5. Run the application (`dotnet run` or F5).

---

### Spanish Version

# Módulo Backend para el Sistema de Votación en Blockchain  
**Desarrollador**: David Tacuri

### Resumen  

Este módulo backend realiza las siguientes tareas:
1. Expone APIs para subir actas de escrutinio escaneadas.
2. Envía las actas a IPFS mediante API REST.
3. Genera y exporta validadores en Aiken para contratos inteligentes de Cardano.
4. Integra el flujo de escaneo con scripts Plutus para validación en cadena.
5. Proporciona diagnóstico para validar el formato de datos antes de enviar.

### Características Principales

- **Interfaz API**: Para subir y procesar actas de escrutinio.
- **Integración con IPFS**: Envío de imágenes y datos.
- **Generación de Validadores**: Usa Aiken para compilar y exportar.
- **Validación On-Chain**: Utiliza Plutus para validaciones seguras.
- **Herramienta de Diagnóstico**: Verifica integridad del datum.

### Aspectos Técnicos

- **Lenguaje**: C#
- **Framework**: .NET 8.0
- **Arquitectura**: APIs RESTful

### Resumen de Endpoints

| Método | Endpoint | Descripción |
|--------|----------|-------------|
| GET | /api/Acta/{id} | Obtener datos de acta |
| POST | /api/Acta/{id}/escaneo | Enviar acta al backend |
| POST | /api/Acta/validadores/exportar | Exportar validadores Plutus |
| POST | /api/Acta/{id}/escaneo-plutus | Validar acta con Plutus |
| POST | /api/Acta/diagnose-datum | Diagnóstico del datum |

### Requisitos Previos

- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- Visual Studio 2022 o superior

### Procedimiento de Instalación

1. Clonar el repositorio:  
```bash
git clone https://github.com/democracyonchain/asuncion-backend-onchain.git
```
2. Abrir la solución en Visual Studio.
3. Restaurar paquetes NuGet.
4. Compilar el proyecto.
5. Ejecutar la aplicación (`dotnet run` o F5).