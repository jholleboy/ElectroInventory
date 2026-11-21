# Tipos, propriedades e ações

O tipo comercial e a classe C# são dimensões distintas. O radial agrupa por `ItemType` e `ItemSubType`. `MenuCategory` atende alguns menus de interação. Os tipos da RC2 são None, Drinks, Food, Drugs, Equipment, Services, Vehicles, Weapons, LicensePlates, Valuables, Meals, Combos, Tobacco, Jewelry, Cellphone e Paraphernalia. Consulte a lista de subclasses no [índice](SOURCE_INDEX.md).

| Família | Propriedades e ações |
| --- | --- |
| Definição base | Nome interno, descrição, tipo/subtipo, categoria, medida, unidades por pacote, modelo/embalagem, ilicitude de posse/uso, consumo na compra e custo por uso. `UseItem`, `ConsumeItem` e `ConsumeItemSlow` na classe base retornam falso. |
| Pilha | `InventoryItem.Amount` e `RemainingPercent`. Mostrar quantidade e fração com unidade original. `InventoryItem.Description` incorpora a descrição da definição e dados da pilha. |
| Consumíveis | `ConsumableItem`; subclasses `FoodItem`, `DrinkItem`, `IngestItem`, `InhaleItem`, `InjectItem`, `SmokeItem` e `PipeSmokeItem`. Usos específicos, prop/anim, efeitos de necessidades e intoxicantes conforme classe e configuração. Medicamento e narcótico podem ser subtipos da mesma infraestrutura. |
| Equipamento | `EquipmentItem` e `BodyArmorItem` passam por executor próprio de uso. Binóculos, lanterna, rádio, guarda-chuva, detector e celular têm implementações específicas; expor estado somente quando obtido do sistema real. `ClothingItem.UseItem` é distinto do preview e da compra de roupa. |
| Ferramentas e acessórios | Furadeira, pá, martelo, alicate, chave de fenda, fita, isqueiro, bong, cachimbo e papel têm subclasses. Atividades e requisitos podem consumir outros itens ou depender do contexto; a ação deve seguir o override. |
| Armas e munição | `WeaponItem` modela o catálogo. Estado equipado, arma nativa, munição, componentes e modo de disparo pertencem a `WeaponEquipment`, ao inventário de armas e ao GTA V. `StoredWeapon` é a representação de armazenamento. Equipar/desequipar/recarregar não são ações genéricas do radial de itens. |
| Placa/identificação e bens especiais | `LicensePlateItem` tem uso e venda próprios. `ValuableItem`, `TrophyItem`, `VehicleItem` e `HotelStayItem` têm semântica própria. Documentos e licenças dos menus de identificação não devem ser fabricados como pilhas pessoais. |
| Descarte | `ActivityManager.DropInventoryItem → ModItem.DropItem → ItemDroppingActivity.Exit`: animação, cancelamento e remoção. O fluxo auditado termina em remoção e mensagem, sem gerar um objeto recolhível. |
| Operações contextuais | Retirar/depositar pelos menus de armazenamento. Comprar/vender por catálogo e sessão. Fabricar via `CraftingMenu` e `Craft`; inspeção e saque de NPC por interação própria. Nenhuma classe de item deve herdar um conjunto uniforme de ações visuais. |

Sem atributo geral confirmado de peso/capacidade da pilha ou flag universal de proteção de missão. Projetar ações a partir das permissões reais do contexto, sem inferir pelo subtipo. Apresentação PT-BR vem de `DisplayName`, `DisplayDescription`, `GetDisplayMeasurementName` e catálogo central. Não usar textos traduzidos, `ModelItemID`, posição na grade ou texto de descrição como chave de pilha.
