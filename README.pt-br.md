# Omega Music Player

[![en](https://img.shields.io/badge/lang-en-red.svg)](README.md)
[![pt-br](https://img.shields.io/badge/lang-pt--br-green.svg)](README.pt-br.md)

Um reprodutor de música local para Windows com temas personalizáveis que combina funcionalidade intuitiva e design moderno. Desenvolvido com Avalonia UI.

## Principais Funcionalidades

- **Controles de Reprodução**: Play, pause, voltar, pular, avançar, retroceder, aleatório, repetir
- **Gerenciamento de Playlists**: Crie, edite e organize suas playlists
- **Sistema de Favoritos**: Marque músicas como favoritas para acesso rápido na playlist de favoritos
- **Busca**: Pesquise qualquer música na sua biblioteca
- **Arrastar e Soltar**: Reordene músicas e playlists de forma intuitiva
- **Gerenciamento de Fila**: Adicione músicas à fila, toque a próxima ou adicione a playlist
- **Estatísticas da Biblioteca**: Visualize propriedades detalhadas sobre suas músicas

## 🎵 O Que Torna o Omega Music Player Diferente

### Sistema Multi-Perfil
Crie até 20 perfis com biblioteca compartilhada, mas configurações e preferências individuais.
- Biblioteca, idioma e opção de busca de imagens de artistas são compartilhadas entre todos os perfis
- Todas as outras configurações são únicas para cada perfil

Perfeito para:
- Computadores compartilhados com múltiplos usuários
- Separar músicas de trabalho e pessoais
- Criar perfis temáticos para diferentes momentos

### Personalização Flexível da Interface
- **Modos Escuro/Claro**: 2 opções pré-definidas de modo escuro e claro
- **Temas Personalizados**: Crie temas personalizados com suporte a gradientes

### Quatro Modos de Visualização Únicos
Alterne entre estilos de visualização de acordo com sua preferência:
- **Visualização em Lista**: Layout clássico e denso em informações com todos os detalhes visíveis
- **Visualização em Cards**: Cards no estilo álbum com arte da capa e informações essenciais
- **Visualização em Imagens**: Foco na arte da capa com sobreposição mínima de texto
- **Visualização em Imagens Redondas**: Miniaturas circulares distintas

### Gerenciamento Inteligente da Biblioteca
- **Sistema de Lista Negra**: Oculte pastas ou arquivos da sua biblioteca sem deletar suas informações
- **Imagens de Artistas**: Busca artwork de artistas de fontes online (requer internet, pode ser desabilitado)
- **Indexação em Segundo Plano**: Escaneie sua biblioteca de forma automática quando houver mudanças sem interromper a reprodução

### Recursos Aprimorados de Escuta
- **Temporizador**: Configure desligamento automático da reprodução após uma duração especificada
- **Exibição de Letras**: Visualize letras das músicas enquanto escuta (quando disponível nos metadados)
- **Pausa Dinâmica**: Pausa automaticamente a reprodução quando outro áudio é reproduzido (ex: vídeos, chamadas), depois retoma quando terminar (opcional, desabilitado por padrão)

### Otimizado para Performance
- Gerencia bibliotecas grandes (mais de 10.000 músicas) com facilidade
- Carregamento rápido da biblioteca

### ⚠️ Formatos Suportados
- MP3, AAC, M4A

## 🚀 Primeiros Passos

### Requisitos
- Windows 10/11 (64-bit)
- Visual C++ Redistributable

### Instalação
1. **Instalar Pré-requisitos**: Baixe e execute o **VC_redist.x64.exe** da [última versão](https://github.com/erick-panse/OmegaMusicPlayer/releases/latest) ou diretamente da [Microsoft](https://aka.ms/vc14/vc_redist.x64.exe)
2. **Baixar**: Baixe o **OmegaMusicPlayer.zip** da [última versão](https://github.com/erick-panse/OmegaMusicPlayer/releases/latest)
3. **Extrair**: Descompacte para o local de sua preferência
4. **Executar**: Inicie o **OmegaMusicPlayer.exe**

## Stack Tecnológico

- **Framework**: Avalonia UI (C#, .NET 8.0)
- **Banco de Dados**: PostgreSQL (Embedded via [MysticMind.PostgresEmbed](https://github.com/mysticmind/mysticmind-postgresembed))
- **ORM**: Entity Framework Core
- **Motor de Áudio**: NAudio
- **Metadados**: TagLibSharp
- **Arquitetura**: MVVM com CommunityToolkit.Mvvm

## Licença

Omega Music Player é licenciado sob a Licença MIT. Consulte o arquivo [LICENSE](LICENSE) para mais detalhes.

Copyright © 2025 Erick Panse

## Bugs e Problemas

Encontrou um bug? Por favor, reporte na página de [GitHub Issues](https://github.com/erick-panse/OmegaMusicPlayer/issues).

## 🤝 Contribuindo

Contribuições são bem-vindas! Sinta-se livre para enviar issues e pull requests.

Para mudanças importantes, por favor abra uma issue primeiro para discutir o que você gostaria de mudar.
