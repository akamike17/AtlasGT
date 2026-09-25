# Connector Documentation

## Available Protocols
- **Modbus**: Supports TCP and RTU. Configuration requires Slave ID and Register Map.
- **MQTT**: Pub/Sub model. Requires Broker URL, Topic, and Credentials.
- **OPC UA**: Industrial standard. Requires Endpoint URL and Security Policy.
- **Serial**: RS-232/485 communication. Requires COM port and Baud rate.

## Adding a New Connector
1. Reference `AtlasGT.Connectors.Abstractions`.
2. Implement the `IConnector` interface.
3. Register the connector in the Dependency Injection container within `AtlasGT.Infrastructure`.
