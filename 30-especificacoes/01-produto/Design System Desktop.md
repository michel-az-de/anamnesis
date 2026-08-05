---
title: Design System Desktop
aliases: [Anamnesis Desktop DS, Command Deck]
tags: [produto, design, desktop, design-system]
type: note
created: 2026-08-05
updated: 2026-08-05
status: evergreen
summary: Tokens, componentes, motion e regras visuais da experiência desktop do Anamnesis.
related: ["[[Identidade Visual]]", "[[SPEK-029 Polimento Visual e Motion Desktop]]"]
---

# Design System Desktop

## Direção

**Command Deck** combina a precisão de uma ferramenta profissional com a presença visual de uma interface de game bem resolvida.

```text
memória + foco + sistema local
          |
          v
contraste preciso + resposta imediata + evidência visível
```

Não é cyberpunk. Não é neon. Não é fantasia. Não usa transparência. O foco é criar uma interface sólida com cobre, grafite azulado, tipografia precisa, bordas controladas e movimento contextual.

## Princípios de experiência

| Princípio | Tradução visual | Regra de decisão |
| --- | --- | --- |
| Memória viva | Histórico, continuidade e estados persistentes | O conteúdo comunica memória, não o cenário |
| Sistema confiável | Estados, métricas e console legíveis | Saúde e erro sempre têm texto, ícone ou forma além da cor |
| Foco imediato | Cobre na ação e no item selecionado | Apenas uma ação primária domina cada contexto |
| Resposta tátil | Hover, pressão, foco e transição curta | Toda interação responde em até 250 ms |
| Local por natureza | Moldura Windows e dados operacionais | A interface continua útil sem rede ou motion |

## Hierarquia visual

```text
canvas sólido
    -> chrome e navegação persistentes
        -> superfícies de trabalho
            -> conteúdo e métricas
                -> foco, seleção e ação
```

O cobre sobe um elemento na hierarquia. O verde comunica saúde. O coral é reservado para gravação ativa, perigo ou interrupção. Nenhum brilho é puramente decorativo.

## Auditoria inicial

| Área | Estado anterior | Diagnóstico |
| --- | ---: | --- |
| Cores semânticas | 18 tokens | Boa base |
| Espaçamentos | 52 valores locais | Sem escala documentada |
| Tipografia | 14 instanciações locais | Hierarquia parcial |
| Geometria | 2 usos de cantos | Interface predominantemente quadrada |
| Hover | 1 estado explícito | Resposta interativa pobre |
| Motion | 0 transições | Navegação instantânea e plana |
| Elevação | 1 borda simples | Pouca profundidade |

## Fundações

### Cor e material

| Token | Uso |
| --- | --- |
| `Fundo` | Base profunda ou clara |
| `Canvas` | Base sólida da área de trabalho |
| `Painel` | Cartões e linhas comuns |
| `PainelElevado` | Chrome, navegação e superfícies persistentes |
| `PainelHover` | Resposta a ponteiro e seleção |
| `Sombra` | Separação de planos |
| `BordaForte` | Foco, seleção e separação de alto contraste |
| `DestaqueSuave` | Fundo sólido de foco e ação principal |
| `PositivoSuave` | Fundo sólido de saúde e sucesso |

Todos os tokens de superfície possuem alpha 255. Transparência não é uma variante do sistema.

### Geometria e espaçamento

| Escala | Valores |
| --- | --- |
| Espaço | 4, 8, 12, 16, 24, 32 px |
| Raio | 8, 12, 18, 24 px |
| Elevação | 0, 1, 2, 3 |
| Borda | 1 px padrão, 2 px para foco |

### Elevação e material

| Nível | Receita | Uso |
| --- | --- | --- |
| 0 Canvas | Cor sólida sem decoração | Fundo da janela |
| 1 Base | Painel opaco, borda sutil e raio 8 ou 12 | Linhas e cartões comuns |
| 2 Elevated | Painel opaco de maior contraste e borda forte | Gravação, métricas e blocos de foco |
| 3 Chrome | Plano opaco persistente e separação nítida | Cabeçalho, navegação e console |

Mica, Acrylic, blur e vidro ficam desativados. A moldura Windows usa cor sólida alinhada ao chrome da aplicação.

### Tipografia

| Papel | Família | Tamanho base |
| --- | --- | ---: |
| Display | Manrope, Segoe UI Variable Display, Segoe UI | 21 px |
| Interface | Inter, Segoe UI Variable Text, Segoe UI | 10 px |
| Dados | Cascadia Mono, Consolas | 9 px |
| Métrica | Cascadia Mono, Consolas | 17 px |

### Motion

| Token | Duração | Uso |
| --- | ---: | --- |
| `Faster` | 83 ms | Pressionar e foco |
| `Fast` | 167 ms | Hover e mudança de estado |
| `Normal` | 250 ms | Entrada e navegação |

Curva de entrada: desaceleração cúbica `1 - (1 - t)^3`. O conteúdo entra 24 px na direção da leitura e estabiliza sem rebote.

### Coreografia

| Evento | Sequência | Limite |
| --- | --- | ---: |
| Hover | Mudança de painel e borda | 167 ms |
| Pressionar | Compressão cromática, sem alterar layout | 83 ms |
| Navegar | Página entra 24 px e estabiliza | 250 ms |
| Gravação | Atualização dos medidores e indicador | Somente durante captura |
| Motion reduzido | Atualização direta | 0 ms |

## Componentes

| Componente | Variantes | Estados |
| --- | --- | --- |
| Superfície | Base, Elevated, Chrome, Navigation, Console | Default, hover, active |
| Botão | Primary, Secondary, Danger, Ghost | Default, hover, pressed, focus, disabled |
| Navegação | Default, selected | Default, hover, selected, focus |
| Campo de texto | Default | Default, hover, focus, disabled, error |
| Select | Default | Default, open, focus, disabled |
| Toggle | On, off | Default, hover, focus, disabled |
| Indicador | Healthy, recording, processing, error | Static, pulse |
| Console | Info, warning, error | Default, selected |

### Anatomia dos componentes

| Componente | Partes obrigatórias | Restrições |
| --- | --- | --- |
| Cartão | Superfície sólida, borda, conteúdo, acento opcional | Um único acento e no máximo dois níveis tipográficos |
| Botão de ação | Ícone opcional, rótulo, fundo e foco | Altura mínima de 38 px e foco independente do hover |
| Navegação | Ícone, rótulo, marcador selecionado | Marcador cobre somente no destino atual |
| Métrica | Rótulo, valor monoespaçado, contexto | Nunca depender apenas da cor |
| Medidor | Rótulo, trilha, segmentos e valor semântico | Animação somente durante captura ativa |
| Console | Horário, nível, componente, evento e mensagem segura | Conteúdo de reunião e segredos são proibidos |

### Matriz de estados

| Estado | Superfície | Borda | Conteúdo |
| --- | --- | --- | --- |
| Default | Material base | Neutra | Contraste normal |
| Hover | Painel mais claro | Realce discreto | Sem deslocamento |
| Pressed | Painel mais denso | Acento reduzido | Resposta em 83 ms |
| Focus | Sem mudança obrigatória | Contorno de 2 px | Navegação por teclado preservada |
| Selected | Material forte | Marcador cobre | Rótulo de alto contraste |
| Disabled | Material opaco reduzido | Neutra | Contraste secundário e sem glow |
| Error | Material base | Coral controlado | Mensagem textual obrigatória |

## Composição de tela

| Região | Medida de referência | Comportamento |
| --- | ---: | --- |
| Janela | 1180 x 760 px | Redimensionável, mínimo 980 x 640 px |
| Chrome superior | 62 px | Persistente |
| Navegação lateral | 208 px | Persistente no desktop |
| Margem de conteúdo | 24 a 32 px | Reduz antes de comprimir controles |
| Controle de navegação | 184 x 46 px | Alvo confortável para mouse e teclado |

- Priorizar uma leitura em Z: título, estado principal, conteúdo e ação.
- Manter o console denso e monoespaçado, com filtros fora da área de logs.
- Não colocar mais de quatro cartões de métrica na mesma linha.
- Em largura mínima, reduzir margens e colunas antes de esconder informação.
- Reservar a superfície `Elevated` para o objetivo principal da página.
- Inputs ocupam uma linha visual única, com rótulo acima e ajuda abaixo somente quando necessária.
- A página inicial usa uma ação primária, cartões compactos e uma lista dominante.

## Ícones e linguagem

- Usar ícones simples, consistentes e reconhecíveis, nunca como única descrição.
- Preferir frases curtas em PT-BR, verbos no infinitivo para ações e estados no presente.
- Usar nomes técnicos somente quando ajudam diagnóstico, especialmente no console.
- Evitar excesso de caixa alta. Reservar para sinais operacionais curtos.

## Regras de uso

- Não usar brilho, transparência, blur, grade ou decoração ambiente.
- Separar planos com cor sólida e borda, não com sombra pesada.
- Usar motion para explicar mudança de estado ou posição.
- Evitar animação contínua em texto, listas ou conteúdo de leitura.
- Manter contraste independente do material de fundo.
- Manter todos os controles acessíveis por teclado.

## Acessibilidade e fallback

- Se animações da área cliente estiverem desabilitadas, transições e pulso param.
- Superfícies são sempre opacas, independentemente das preferências do sistema.
- Tema claro e escuro possuem tokens próprios.
- Foco deve ser visível por borda e não somente por cor.
- O conteúdo nunca depende de transparência, brilho ou motion para comunicar estado.

## Governança

- Novos componentes devem reutilizar tokens antes de introduzir valores locais.
- Uma nova cor semântica exige uso documentado nos temas claro e escuro.
- Uma nova animação precisa de propósito, duração e comportamento com motion reduzido.
- Novos tokens de superfície devem falhar em teste quando o alpha for diferente de 255.
- Alterações de componente devem cobrir pelo menos default, hover, foco, disabled e tema claro.
- Capturas dark e light em tamanho de referência fazem parte da evidência visual.
- Dependências visuais externas ou migração de framework exigem ADR.
