# Dispositivos da Rede

## Infraestrutura
- Gateway
  - IP: 172.16.32.1
  - Tipo: roteador
  - Monitorar: sim
  - Critico: sim

## Casa inteligente
- Home Assistant
  - IP: 172.16.32.73
  - Tipo: automacao
  - Monitorar: sim
  - Critico: sim

## Dispositivos pessoais
- PC Nexus
  - IP: 127.0.0.1
  - Tipo: computador
  - Monitorar: sim
  - Critico: nao

## Regras
- Alertar se Home Assistant cair.
- Alertar se Gateway cair.
- Nao registrar conteudo de trafego, apenas status e metadados.
