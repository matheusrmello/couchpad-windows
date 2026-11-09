# Tokens de tema

## Cores (tema escuro)

| Token | Valor | Uso |
|---|---|---|
| `CouchPad.Background` | `#0A0A0B` | fundo da janela |
| `CouchPad.Surface` | `#17181A` | fundo de card |
| `CouchPad.SurfaceRaised` | `#1F2023` | controles, selects, chips |
| `CouchPad.Border` | `#2A2B2F` | borda de card e divisor |
| `CouchPad.BorderStrong` | `#3A3B40` | foco/hover de borda |
| `CouchPad.TextPrimary` | `#F2F3F5` | títulos e valores |
| `CouchPad.TextSecondary` | `#9AA0A6` | labels e ajuda |
| `CouchPad.TextDisabled` | `#5F6368` | desabilitado |
| `CouchPad.Accent` | `#2FD07A` | marca, status ativo, foco |
| `CouchPad.AccentMuted` | `#123D28` | fundo de pill/realce (contraste AA com `Accent`) |
| `CouchPad.Info` | `#2F80ED` | controle secundário (opcional) |
| `CouchPad.Warning` | `#F2C94C` | atenção |
| `CouchPad.Danger` | `#EB5757` | erro |

> Use `DynamicResource`. Não escreva hex nas Views.

## Tipografia

| Token | Tamanho/linha | Peso | Uso |
|---|---|---|---|
| `CouchPad.Font.Display` | 28/34 | SemiBold | marca/título grande |
| `CouchPad.Font.H1` | 20/26 | SemiBold | título de página |
| `CouchPad.Font.CardTitle` | 15/20 | SemiBold | título de card |
| `CouchPad.Font.Body` | 13/18 | Regular | texto geral |
| `CouchPad.Font.BodyStrong` | 13/18 | SemiBold | valores |
| `CouchPad.Font.Caption` | 11/14 | Regular | ajuda/legenda |
| `CouchPad.Font.Mono` | 12/16 | Regular | chips/combo |

Fonte: `Segoe UI Variable` no Windows; fallback `Inter`, `Segoe UI`.

## Espaçamento

Escala: `4, 8, 12, 16, 20, 24, 32`.

- Padding de card: `20–24`.
- Gap entre cards: `16`.
- Gap interno de formulário: `12`.
- Altura de controle: `36`.
- Altura de botão pequeno: `32`.

## Raios

| Token | Valor | Uso |
|---|---|---|
| `CouchPad.Radius.Control` | `8` | botões, inputs |
| `CouchPad.Radius.Card` | `12` | cards |
| `CouchPad.Radius.CardLarge` | `14` | card herói |
| `CouchPad.Radius.Pill` | `999` | pill/chip |

## Borda e elevação

- Cards: `1px` de borda; sem sombra.
- Controles: `1px` de borda; fundo `SurfaceRaised`.
- Foco: anel de `2px` em `Accent`, sem deslocar layout.

## Exemplo XAML

```xml
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <SolidColorBrush x:Key="CouchPad.Background" Color="#0A0A0B"/>
  <SolidColorBrush x:Key="CouchPad.Surface" Color="#17181A"/>
  <SolidColorBrush x:Key="CouchPad.SurfaceRaised" Color="#1F2023"/>
  <SolidColorBrush x:Key="CouchPad.Border" Color="#2A2B2F"/>
  <SolidColorBrush x:Key="CouchPad.TextPrimary" Color="#F2F3F5"/>
  <SolidColorBrush x:Key="CouchPad.TextSecondary" Color="#9AA0A6"/>
  <SolidColorBrush x:Key="CouchPad.Accent" Color="#2FD07A"/>

  <CornerRadius x:Key="CouchPad.Radius.Card">12</CornerRadius>
  <Thickness x:Key="CouchPad.Padding.Card">20</Thickness>
  <x:Double x:Key="CouchPad.Gap">16</x:Double>
</ResourceDictionary>
```

## Regras

- Novo token só se o existente não cobrir o caso.
- Nome semântico, não visual (`Accent`, não `Green`).
- Tokens de estado derivam do acento: hover 10% mais claro, pressed 10% mais escuro.
- Tema claro, se existir, deve redefinir os mesmos tokens, não as Views.
