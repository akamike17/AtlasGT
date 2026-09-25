# AtlasGT Architecture Guide

## Overview
AtlasGT is an Industrial IoT Gateway designed for high-performance data acquisition, normalization, and historization.

## Core Components
- **AtlasGT.Domain**: Contains the business entities, interface definitions, and domain logic.
- **AtlasGT.Application**: Implements use cases and orchestrates the flow between domain and infrastructure.
- **AtlasGT.Infrastructure**: Handles external concerns like Database access (EF Core), File System, and Security.
- **AtlasGT.Connectors**: A modular system for industrial protocols (Modbus, MQTT, OPC UA, etc.).
- **AtlasGT.Normalization**: Ensures data from disparate sources is converted to a standard internal format.
- **AtlasGT.Historian**: Manages the time-series storage of acquired data.
- **AtlasGT.Api & Web**: Provides the RESTful interface and administrative dashboard.

## Data Flow
Device $\rightarrow$ Connector $\rightarrow$ Normalization $\rightarrow$ Application $\rightarrow$ Historian $\rightarrow$ API/Web
