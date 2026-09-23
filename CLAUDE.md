# RPG con combate estilo MUD (Unity + C#)

## Convenciones
- **Conversación con el desarrollador: en español.**
- **Todo el contenido del juego en inglés:** comandos, emotes, nombres de skills, textos en pantalla, identificadores de código.
- Desarrollador con experiencia en C# / .NET (ASP.NET Core, EF Core). No hace falta explicar el lenguaje; sí conviene explicar conceptos propios de Unity (MonoBehaviour, ScriptableObjects, escenas, prefabs).

## Contexto
- Idea: un RPG sencillo pero con un combate peculiar, heredado de un MUD que el desarrollador ya estuvo diseñando (remake web de un MUD ambientado en El Señor de los Anillos, con ASP.NET Core + SignalR, con mecánicas de combate y durabilidad ya pensadas).
- Motor elegido de momento: **Unity** (C#). Se comentó Godot (también con C#) como alternativa ligera para 2D.
- Consejos acordados: empezar pequeño, en 2D y con placeholders (cuadrados o assets gratuitos tipo Kenney). **Primer objetivo: prototipar SOLO el combate.** Controlar el alcance (inventario, tiendas, misiones, guardado… van después).

## Reglas de combate (DECIDIDAS)
1. **Inicio:** el combate empieza cuando un jugador (o NPC) ataca con el comando `kill <target>`.
2. **Rondas fijas y automáticas.** Las rondas se suceden solas a intervalos regulares, sin que el jugador tenga que hacer nada.
3. **En cada ronda atacan los 2 combatientes.**
4. **Iniciativa fija:** quien lanzó `kill` ataca primero en **todas** las rondas del combate. Si su ataque mata al otro, el otro ya no responde en esa ronda.
   - Los NPC agresivos que atacan al jugador tienen ellos la iniciativa.
   - Nota de equilibrio: atacar primero es una ventaja real en combates cortos (es intencionado).
5. **Fin del combate:** muere uno de los dos, o el jugador **se aleja** del NPC (fuera de rango; en Unity se comprueba la distancia en cada ronda), o se usa el comando de huida (ver más abajo).
6. **Resolución de cada ataque:**
   1. Tirada de acierto (precisión del atacante contra evasión del defensor, con modificadores de skills, buffs y debuffs). Si falla → emote `miss`.
   2. Tirada de crítico → `critical hit`.
   3. Daño = base (stats + arma + skills) con **componente aleatorio siempre** (p. ej. ±20%), para que no salga siempre el mismo golpe; luego se resta la armadura/defensa.
   4. Se clasifica el golpe en uno de los **6 niveles normales** (o crítico) y se muestra el emote.
7. **Habilidades especiales:**
   - Se pueden lanzar **en cualquier momento** del combate (quedan en cola).
   - **Consumen 1 turno:** en la siguiente acción del personaje sustituyen a su ataque normal de esa ronda.
   - **Cooldown en turnos/rondas, NO en tiempo.**
   - Cada **profesión** tiene sus habilidades: unas de combate y otras que no son de combate.
8bis. **Timing:** cada turno dura **1 segundo**; una ronda completa (ambos combatientes actúan) dura **2 segundos**. El motor (`CombatEngine`) no controla el tiempo real — expone `PlayFirstTurn()`/`PlaySecondTurn()` para que quien lo llame (timer/corrutina en Unity, `Task.Delay` en el prototipo de consola) espacie cada turno; `PlayRound()` sigue existiendo para ejecutar una ronda entera de golpe (usado en la simulación masiva, que no debe tener delays reales).
8ter. **Huida (versión simple por ahora):** un comando de huida **sin penalización**. Llama a `CombatEngine.RequestFlee()`, que **no corta la ronda en curso** — esa ronda se completa entera (los 2 turnos) y el combate se detiene recién al empezar la siguiente ronda. No hay ganador ni derrota, solo se sale del combate.
8quater. **Sangrado (caído):** si un golpe deja el HP en ≤ 0 pero **no por debajo de -15**, el objetivo no muere: queda en el suelo *bleeding and in need of bandages* (`LifeState.Bleeding`). Si el HP queda en **≤ -15**, muerte instantánea (`CombatConstants.InstantDeathHp`). Constantes en `CombatConstants`: `BleedRounds = 10`, `BandageHealFraction = 0.10`.
   - Mientras alguien sangra el combate se **pausa** (nadie ataca) pero las rondas siguen: cada ronda repite el texto y baja el contador; a las 10 rondas **se desangra y muere** (`CombatEndReason.BledOut`).
   - `kill <target>`: **solo quien está en combate con él** puede rematarlo (`CombatEngine.FinishOff`). Alguien que no esté en combate con él **no puede usar `kill`**, solo `bandage <nombre>`.
   - `bandage` (`CombatEngine.Bandage` / `Combatant.Stabilize`): solo funciona si hay alguien sangrando. En combate con él basta `bandage`; fuera de combate hace falta el nombre (`bandage rat`). Estabiliza: deja de sangrar, recupera el **10% del HP máximo**, **el combate se detiene** (sin ganador). Después puede volver a atacar y se repite lo mismo si vuelve a caer.
   - Si el que cae es el jugador: no puede hacer nada (ni `kill`, ni `bandage`, ni `flee`) hasta desangrarse o `reset`; por ahora nadie puede vendarlo (no hay multijugador) y el NPC no lo remata solo.
   - `flee` con el oponente sangrando: se sale del combate y el NPC sigue sangrando (`BleedOutAsync` en `Program.cs` lleva la cuenta, ya sin motor). Se reutiliza el mismo NPC (`activeNpc`) al repetir `kill <id>` mientras esté vivo o sangrando; si murió, reaparece uno nuevo. Solo se recuerda **un** NPC activo.
   - El motor emite `OnNarration` para estos textos (perspectiva "You" según `IsPlayer`) y protege sus métodos con un `lock`, porque `kill`/`bandage` llegan desde otro hilo que el bucle de rondas.
8quinquies. **Durabilidad:** cada arma/armadura arranca en `CombatConstants.MaxDurability = 1000`. Un golpe **que conecta** (no en miss) baja 1 punto, siempre, sin importar el tier ni si es crítico: el arma del atacante y **cada pieza** de la armadura del defensor. En 0 el item queda "roto": se puede seguir usando/llevando puesto pero sus atributos de combate leen como 0 (`Weapon.EffectiveHit/Damage/CritChanceBonus/CritPower`, `Armor.EffectiveAbsorb/Deflect`).
   - **Herrero y reparación (DECIDIDO e implementado):** `World.Blacksmith` (mismo patrón que `Shop`/`Healer`/`Trainer`), en **Millford Forge** (nueva habitación, al oeste de la tienda: plaza→oeste→tienda→oeste→forja). `Weapon`/`Armor` implementan `Combat.IDurableItem` (interfaz compartida para que la consola trate arma/armadura igual sin duplicar lógica). Comando `repair [item]`: sin argumento muestra el menú (también accesible con `look blacksmith`/`look <nombre del herrero>`) con la condición y el precio de cada pieza que tengas (arma empuñada si no es natural, armas envainadas, armadura puesta, armadura en mano); `repair <item>` paga y repara. **Costo: `CombatConstants.RepairCostFraction = 0.5`** del `Value` del item si está completamente roto (0 durabilidad), proporcionalmente menos si le falta menos (`Value × 0.5 × (faltante/MaxDurability)`, mínimo 1 gold) — **número tal cual lo confirmó el desarrollador** (opción "50% del Value" sobre 25%/100%). **Solo se repara una vez en la vida del item** (`HasBeenRepaired`): un segundo intento se rechaza explícitamente, tanto en el menú como al intentar `repair`.
   - **Comando `condition <item>` (DECIDIDO e implementado):** funciona en cualquier lugar (no hace falta estar con el herrero) — da una **descripción cualitativa** de cuánto le queda al item (`pristine` / `good` / `fair` / `worn` / `poor` / `terrible condition`, o `broken` aparte si llegó a 0), **no el número exacto** (a diferencia del gold). Los umbrales y el texto son placeholders genéricos del desarrollador de IA, fáciles de cambiar. También avisa si ya fue reparado.
   - **`MaxDurability` es por item, no global (bug corregido):** `Weapon`/`Armor` ya soportaban un `MaxDurability` propio por instancia, pero `Durability` se inicializaba con un field initializer fijo a `CombatConstants.MaxDurability` (1000) — como los initializers de campo corren *antes* de que el object-initializer (o el JSON) aplique un `MaxDurability` distinto, cualquier item con menos de 1000 quedaba con `Durability` arrancando en 1000 igual (tardaba 1000 golpes en romperse en vez de los que tocaran). Arreglado con un backing field nullable (`_durability`) que cae a `MaxDurability` de forma perezosa la primera vez que se lee, ya con el valor real aplicado. Verificado a mano: un Knife con `maxDurability: 400` ahora se rompe ~400 golpes conectados, no 1000.
   - **Durabilidad de la tienda actual (placeholder, fórmula del desarrollador de IA):** `maxDurability = Value × 50` — Knife 400, Dagger 500, Short Sword 750, Rustic Leather Cap/Bracers 200, Vest 400, Leggings 250, Boots 150, Wooden Shield 300. Más caro = más duradero, confirmado por el desarrollador; el multiplicador (×50) es solo una primera pasada, ajustable.
   - **Un item reparado se nota en su nombre dondequiera que se muestre** (inventario, manos, envainado, puesto): aparece como `"<nombre> (repaired)"`. Es lo único visible **sin** usar `condition` — el resto de la condición (roto o simplemente desgastado, sin reparar) no se ve a simple vista, hay que preguntar con `condition`.
8. Al final de cada ronda: bajan los cooldowns y avanza la duración de buffs y debuffs.

## Sistema monetario e inventario (EN PROGRESO)
- **Moneda:** `gold`, unidad única, sin submonedas. `Combatant.Gold` (+`AddGold`/`SpendGold`/`RestoreGold`), persistido en `CharacterSave`. Comando `gold` para consultar el saldo — a diferencia del HP, el gold **sí se muestra como número exacto**.
- **Filosofía de precios:** el gold debe sentirse valioso — nada de manejar 1000 de gold. 100 de gold ya es una cantidad importante; un item de 200-300 es algo muy valioso en el juego. Números concretos de items pendientes de definir (se irán afinando).
- **Tiendas (DECIDIDO e implementado):** el jugador compra al 100% del precio base (`Weapon.Value`/`Armor.Value`/`Backpack.Value`, en gold) y **vende** a la tienda al **25%** (`CombatConstants.ShopSellFraction`), redondeado, con mínimo 1 gold (para que ni un item de valor 1 se venda por 0) y **tope de 50 gold** (`CombatConstants.MaxSellPrice`, placeholder — número tuyo para ajustar después).
  - `World.Shop` (referenciado desde `Room.Shop`, nullable): listas de `Weapon`/`Armor` + un `Backpack` opcional a la venta. Comprar siempre da una **copia nueva** (el stock no se agota). Comandos `list` (ver stock y precios), `buy <item>` y `sell <item>` — ambos envainan automáticamente el arma en mano si hacía falta (mismo gancho que el botín, ver abajo). `sell` solo vende lo que **no** está equipado (armas envainadas, armadura en mano, items de la mochila) — si lo que se nombra sigue puesto/empuñado, pide quitárselo/envainarlo primero en vez de hacerlo por su cuenta. Cualquier tienda compra cualquier cosa por ahora (no hay todavía tiendas especializadas que solo compren cierto tipo de material).
  - **Millford Trading Post** (`world.millford_gate` → sur → `millford.square` → oeste → tienda): vende Knife/Dagger/Short Sword, un set "Rustic Leather" (Head/Torso/Arms/Legs/Feet, suma actual 18 Absorb / 11 Deflect — bajito a propósito), un Wooden Shield, y el `Traveler's Pack` (mochila inicial, capacidad 2). Todos los números son placeholders bajos: **responsabilidad del desarrollador** ajustarlos después para que no sumen de más.
- **Botín (DECIDIDO e implementado):** los animales (rat/deer/beast) sueltan un material (`Combatant.Loot`, ahora mismo una piel/hide cada uno) al morir — así es como el jugador consigue su primer gold: cazar, lootear, vender. **No se cae directamente al piso**: se queda en el **cadáver** (`World.Corpse`), que permanece en la habitación `CorpseFadeSeconds` (30s) antes de desvanecerse — este plazo es **independiente** de cuánto tarde el NPC en reaparecer (ver el tick de respawn más abajo). Mientras el cadáver existe, **solo puede lootearlo quien peleó con ese NPC** (`Combatant.LootRights`, concedido a quien inicia el combate con `kill`, no solo a quien da el golpe final — así alguien que lo peleó pero no lo remató sigue teniendo derecho). Al desvanecerse el cadáver, lo que quedara sin lootear **cae al piso de la habitación**, donde ya **cualquiera** puede tomarlo (pensado para el futuro multijugador: sin distinción de derechos una vez en el suelo). Comando `take`/`get`/`loot <item>` (sinónimos): primero busca en cadáveres de la sala (con el chequeo de derechos), luego en el piso; exige mochila propia con espacio, y envaina el arma en mano si hacía falta.
- **Sanador (DECIDIDO e implementado):** `World.Healer` (paralelo a `Shop`, en `Room.Healer`). Comando `heal` = **una dosis fija por llamada**: `CombatConstants.HealCostGold = 10` gold → `HealAmountPerUse = 20` HP **y** 20 MP a la vez, no "hasta llenar". Para curarte del todo hay que llamar `heal` varias veces (y pagar cada vez) — con menos de 10 gold, se niega sin más (no hay curación parcial). Además el sanador tiene una **capacidad limitada por tick**: `HealerCapacityPerTick = 200` (= 10 usos), compartida entre quien sea que lo use; al agotarse, `heal` se niega ("worn out for now") aunque tengas gold, hasta el **siguiente tick de 10 minutos** (mismo `RunRespawnTicksAsync` que ya reaparece NPCs — ahora también resetea la capacidad de cada `Healer` del mapa). No funciona sangrando/muerto (eso es cosa de `bandage`). **Millford Healer's House**, arriba (`up`) desde la plaza. Números de ejemplo, ajustables después si hace falta.
- **Regeneración pasiva de HP/MP (DECIDIDO e implementado):** cada **30 segundos** (`RegenTickSeconds` en `Program.cs`, timer propio e independiente del tick de respawn de 10 min) el jugador recupera `CombatConstants.RegenAmountPerTick = 1` HP **y** 1 MP, sin gastar gold ni hacer nada. **Solo el jugador** (los NPC no acumulan regen, son efímeros — se puede extender a futuro). **Sigue corriendo en combate** (la cantidad es tan chica que no cambia el balance de una pelea). **Se pausa si está sangrando o muerto** (`Combatant.IsDown`) — igual que nadie se auto-vendaría, tampoco se autorregenera caído; se reanuda solo al volver a `Alive` (bandage o revivir). `RunPlayerRegenAsync()` en `Program.cs` es el bucle de fondo; no imprime nada (sin narración), solo refresca el prompt vía `LineEditor.Print` con un `write` vacío para que el `HP: x MP: y >` se vea al día sin ensuciar el log de combate.
- **Experiencia (DECIDIDO e implementado, sin leveling todavía):** `Combatant.Experience`, se persiste igual que el gold (a diferencia del equipo). Cada golpe que conecta el jugador da XP = al daño de ese golpe; matar a un NPC suma además un bono plano = su `MaxHp`. En total, matar a algo da ~2x su HP máximo en XP, tal como se decidió. Comando `xp`/`experience` para consultar el total. El leveling en sí (qué hace la XP, curva, etc.) sigue sin implementarse.
- **MP base = 50** (antes 0, ver `data/player.json`) para personajes nuevos. Progresión decidida pero **sin implementar** (no hay leveling todavía): nivel 1 = 50, +10 por nivel (nivel 2 = 60, etc.), igual que ya estaba anotado para el HP.
- **Durabilidad:** ver regla 8quinquies arriba.
- **Bulk e inventario (DECIDIDO e implementado):** `Bulk` es **fraccionario** (`double`): 2 = arma a dos manos o pieza grande, 1 = normal, 1/2 o 1/4 = cosas pequeñas (pieles, colmillos…). **En la mano, cualquier cosa ocupa mínimo 1 bulk completo** (un pelt de 0.25 sigue tomando una mano entera — no se apilan varias cosas sueltas en una sola mano; el apilado fraccionario solo pasa dentro de la mochila).
  - **Manos:** pool de 2 unidades de bulk (`CombatConstants.HandCapacity`, `Combatant.UsedHandBulk`/`FreeHandBulk`). Solo se puede empuñar **un arma a la vez** (no hay dual-wield en el combate todavía, aunque el modelo ya lo permitiría a futuro). No hace falta mochila para llevar algo en la mano — cualquier item suelto (`Combatant.HeldItems`, `TryHoldItem`) puede ir directo a una mano libre.
  - **Funda:** toda arma real (no las naturales tipo Fists/Bite) tiene su funda implícita, **sin costo de bulk**. `Weapon.IsUnarmed` marca las armas "naturales" (Fists/Bite/Antlers/Claws) — no tienen funda, no se pueden desequipar. `Combatant.SheathedWeapons` guarda las armas propias no empuñadas. Comandos: `wield <arma>` (empuña, envaina lo que llevaras antes), `sheath [arma]` (envaina lo empuñado), `hands` (qué llevas en las manos).
  - **Auto-envainado, solo si hace falta:** `Combatant.SheathCurrentWeapon()` es el gancho que usan `buy`/`take` para liberar una mano — pero **solo envaina si de verdad no hay mano libre**; si ya tienes una mano libre, lo nuevo va ahí y el arma se queda empuñada. Comprar un arma (va directo a su funda) o una mochila (se lleva puesta) **nunca** necesita envainar nada, solo comprar/tomar armadura o items sueltos puede necesitarlo.
  - **Armadura sin equipar:** a diferencia de las armas, no tiene "funda" — lo que no está puesto se queda **en la mano** (`Combatant.HeldArmor`), ocupando mínimo 1 bulk de espacio de manos hasta que se equipe o se guarde. `wear <armor>`/`remove <armor>` mueven la pieza entre `HeldArmor` y `EquippedArmor`.
  - **Shield:** un 6º `BodySlot` (además de Head/Torso/Arms/Legs/Feet). A diferencia de los otros 5, **sigue costando una mano incluso puesto** (`Combatant.TryEquipArmor` lo comprueba) — se sostiene activamente, no se lleva atado. `wield shield` funciona como sinónimo de `wear shield` (el comando `wield` primero busca en `SheathedWeapons`, si no encuentra nada busca en `HeldArmor`).
  - **Mochila:** el personaje **nace sin mochila** — se compra en la tienda. Tiene su propia `Capacity` de bulk (mínimo 2 para la inicial; puede haber mochilas más grandes después). Se lleva puesta, sin costo extra, y **solo se puede tener una**. `Combatant.Backpack`/`BackpackItems`, `TryStoreInBackpack`/`UsedBackpackBulk`/`FreeBackpackBulk`.
  - **5+1 slots de armadura** por parte del cuerpo (`BodySlot`) — **Head** (helm), **Torso** (armour/hauberk), **Arms** (vambraces/gauntlets), **Legs** (steel leggings/mail pants), **Feet** (pelt boots, etc.), **Shield** — y una pieza puede ocupar **más de un slot** (ej. unos "mail pants" que incluyen los zapatos = Legs+Feet a la vez). `Armor.Slots` (`HashSet<BodySlot>`, obligatorio) + `Armor.Bulk`. `Combatant.TryEquipArmor` rechaza si algún slot ya está cubierto por otra pieza puesta, o (solo shield) si no hay mano libre; `UnequipArmor` la manda a `HeldArmor`.
  - **Tope de Absorb/Deflect:** `CombatConstants.MaxArmorPoolTotal` es ahora **125** (antes 100) — mismo patrón que los stats (`MaxEffectiveStat`): normal ~100 sumando un set completo (shield incluido), 125 es solo el tope duro de seguridad. Los números concretos de cada pieza siguen siendo responsabilidad de quien diseña el set, no algo que el motor fuerce.
  - Comando `i`/`inventory`: resumen de manos, armas envainadas, armadura puesta, armadura sin equipar en mano, mochila (con bulk usado/total) y gold.
  - Todo esto se persiste en `CharacterSave` (`SheathedWeapons`, `HeldArmor`, `Backpack`, `BackpackItems`, más `Bulk`/`Slots`/`IsUnarmed`/`Value` en las fichas de arma/armadura).
- **Rebalance de armadura:** no es una mecánica del motor — es responsabilidad de quien diseñe cada set de armadura no ponerle números muy altos (ver tope arriba).
- **Latones de basura (pendiente):** en tiendas y otras habitaciones, para descartar items permanentemente.

## Emotes (provisionales, el desarrollador los cambiará)
De más ligero a más fuerte:

| Nivel | Emote |
|---|---|
| Fallo | `miss` |
| 1 | `scratch` / `graze` |
| 2 | `hit` |
| 3 | `hit hard` |
| 4 | `hit very hard` |
| 5 | `inflict massive damage` |
| 6 | `massacre` |
| Crítico | `critical hit` |

(Ortografía corregida respecto al original: "grace"→"graze", "inflit masive"→"inflict massive", "masacre"→"massacre".)

- Habrá **emotes estándar** para todos los golpes.
- **Algunas armas tendrán emotes personalizados**: si el arma tiene emote para ese nivel, se usa el suyo; si no, el estándar.
- Conviene tener varias variantes por nivel para que no se repitan. Formato típico de MUD: `You hit the orc very hard.` / `The orc hits you hard.`

## Arquitectura propuesta
- **Motor de combate en C# puro, sin dependencias de Unity.** Unity solo se encarga de presentar el combate, recoger el input y ejecutar las rondas con un timer o una corrutina.
  - Se puede testear en consola y simular miles de combates para equilibrar.
  - Se podría **reutilizar la misma lógica en el servidor del MUD** (ASP.NET Core).
- Datos (profesiones, skills, armas, enemigos, tablas de emotes): **ScriptableObjects** en Unity, o tablas en BD en el servidor.
- `Random` inyectable (con semilla) para tests reproducibles.

### Esqueleto de referencia
```csharp
public enum HitTier { Graze, Hit, HitHard, HitVeryHard, MassiveDamage, Massacre, Critical }

public class Combat {
    public Combatant First;   // quien usó "kill"
    public Combatant Second;
    public int Round;

    public void PlayRound() {
        Round++;
        Act(First, Second);
        if (!Second.IsDead)
            Act(Second, First);
        EndOfRound();             // cooldowns, duración de buffs/debuffs
        if (First.IsDead || Second.IsDead) Finish();
    }

    void Act(Combatant atk, Combatant def) {
        var skill = atk.TakeQueuedSkillIfReady();
        var result = skill != null
            ? skill.Execute(atk, def, rng)    // consume el turno
            : ResolveAttack(atk, def);
        def.ApplyDamage(result.Damage);
        Emit(result.Emote);
    }
}

HitResult ResolveAttack(Combatant atk, Combatant def) {
    if (rng.NextDouble() > HitChance(atk, def))
        return HitResult.Miss(Emotes.Get(atk.Weapon, EmoteType.Miss));

    bool crit = rng.NextDouble() < CritChance(atk, def);
    var (min, max) = DamageRange(atk);
    int raw = rng.Next(min, max + 1);
    int dmg = Math.Max(0, (int)((crit ? raw * atk.CritMultiplier : raw) - def.Armor));

    HitTier tier = crit ? HitTier.Critical
                        : (HitTier)Math.Min(5, (int)((raw - min) / (float)(max - min + 1) * 6));

    string emote = atk.Weapon.CustomEmotes?.Get(tier) ?? Emotes.Standard.Get(tier);
    return new HitResult(dmg, tier, emote);
}
```

## Stats, skills, arma y armadura (DECIDIDO)
- **4 stats de combate**, escala habitual 0-100 (hasta 125 con buffs, nunca más):
  - Ofensivos: `Strength`, `Coordination`.
  - Defensivos: `Constitution`, `Agility`.
- **4 skills de combate**, misma escala 0-100 (hasta 125 con buffs):
  - Ofensivos: `Aim`, `Attack`.
  - Defensivos: `Defense`, `Dodge`.
- **Arma**, 4 stats:
  - `Hit` (0-100): entra en el cálculo de acierto.
  - `Damage` (0-100): entra en el cálculo de daño no-crítico. El arma más potente llega a 100.
  - `CritChanceBonus`: fracción pequeña (0.1%-0.5%) que se suma al 0.5% base de todos.
  - `CritPower` (0-100): determina en qué parte del rango de daño crítico (31-50) suele caer esta arma.
- **Armadura** (por pieza), 2 stats: `Absorb` (se suma a Constitution+Defense, lado de daño) y `Deflect` (se suma a Agility+Dodge, lado de acierto). Sumando **todas las piezas equipadas**, `Absorb` total tiene tope 100 y `Deflect` total tiene tope 100 (dos "bolsas" independientes).
- **Buffs/debuffs**: modificadores temporales (flat) a cualquiera de las 8 stats/skills, con duración en rondas; bajan al final de cada ronda junto a los cooldowns (regla 8). El valor efectivo de una stat con buffs nunca supera 125.

### Mundo y movimiento (DECIDIDO)
- **Modelo (opción A, grafo plano):** el mundo es un grafo de **habitaciones** agrupadas en **áreas** (`world`, `town`, `cave`, `city`...). Cada habitación tiene un id global `"<area>.<room>"`, nombre, descripción, salidas (`north/south/east/west/up/down` → id de otra habitación) y `details` (cosas mirables). Una salida puede apuntar a **otra área**: así una habitación del mapa mundo es la entrada a un pueblo/cueva, sin mapas anidados. En el JSON, una salida sin punto es de la misma área; `area.room` cruza de área.
- **Proyecto `src/World`** (C# puro, referencia a `Combat`): `Direction`/`Directions` (parseo con abreviaturas n/s/e/w/u/d), `Room`, `Area`, `WorldMap` (estructura estática; valida al cargar que no haya salidas colgantes ni ids duplicados y que haya **exactamente una** habitación `start`), `WorldState` (quién está dónde: NPCs por habitación, `Spawn`/`Remove`/`FindNpc`, con `lock` porque los NPCs mueren y reaparecen desde otros hilos).
- **Datos:** un JSON por área en `src/ConsoleProto/data/areas/` (`world.json`, `millford.json`), cargados por `WorldLoader`. Cada habitación puede tener `spawns: ["rat", ...]` (ids de `data/npcs`) y `"start": true` para la habitación inicial.
- **El mapa nunca se ve con un comando.** Solo existe como un objeto de la habitación (`look map` en la entrada del pueblo, texto ASCII escrito a mano en `details`). Todos los pueblos tendrán un mapa en la entrada, en la posada, la tienda y el lugar más importante.
- **Comandos:** `look` (habitación: nombre, descripción, salidas, quién hay), `look <cosa>` (NPC de la habitación con su `description` + estado, `me`/`self`, o un `detail` de la habitación), movimiento `north/south/east/west/up/down` y `n/s/e/w/u/d`.
- **Moverse en combate = huir:** equivale a `flee` (`RequestFlee`): la ronda en curso se completa, el combate termina y **entonces** el jugador se mueve (`pendingMove`). Si el combate acaba de otra forma (uno muere/se desangra/se vendan), el movimiento se cancela. Si dejas a un NPC sangrando, sigue desangrándose en su habitación (`BleedOutAsync`, solo narra si sigues ahí) y se le puede vendar por nombre al volver.
- **NPCs por habitación:** `kill rat`, `bandage rat`, `look rat`, `shape rat` buscan el NPC **en la habitación actual** (por `Combatant.Id` o por el nombre). Se acabó la limitación de "un solo NPC activo".
- **Tick de respawn (DECIDIDO e implementado):** los NPC **no reaparecen al morir** — hay un **tick global del mundo cada 10 minutos** (`RespawnTickSeconds` en `Program.cs`, un bucle de fondo que arranca al iniciar el proceso, independiente de cualquier sesión de jugador). Cada NPC tiene `Combatant.RespawnTicks` (entero, por defecto 1) = cuántos tics de 10 min hay que esperar tras su muerte; NPCs importantes/raros usarían un número alto (ej. 40 = ~6.5h), los animales actuales están en 1. `WorldState.ScheduleRespawn`/`AdvanceRespawnTick` llevan la cuenta (una lista de respawns pendientes, cada uno con sus tics restantes; cada tick global los baja a todos y reaparece los que llegan a 0). Esto es **independiente** del desvanecimiento del cadáver (30s, ver "Botín" arriba) — un cadáver puede seguir ahí cuando el NPC ya reapareció al lado, si el respawn tarda poco y el cadáver aún no llegó a los 30s.
- **Habitaciones actuales (placeholders, se renombrarán):** `world.millford_gate` (entrada, en el mapa mundo, `start`, salida al sur) → `millford.square` (la plaza, hub central; salidas norte al gate, oeste tienda, sur callejón, este pradera, **arriba sanador, abajo entrenador**) → `millford.shop` (tienda, oeste de la plaza; salida oeste hacia la forja), `millford.forge` (herrero, al oeste de la tienda), `millford.healer` (sanador, arriba), `millford.trainer` (entrenador, abajo), `millford.alley` (rat, sur), `millford.meadow` (deer, este) → `millford.thicket` (beast, más al este desde meadow). Cada animal tiene su propia habitación. El mapa ASCII de `world.millford_gate` (`look map`) se mantiene sincronizado a mano con este layout — **si se añade/mueve una habitación de Millford, hay que actualizar también ese `details.map`**.
- **Pendiente:** más habitaciones y áreas, NPCs que se mueven por áreas restringidas (`roam`), puertas, la lista de profesiones/habilidades, entrenamiento de skills.

### Persistencia del personaje (DECIDIDO)
- Al arrancar hay dos comandos: **`create`** (personaje nuevo) y **`continue`** (pide el nombre y entra donde se quedó). Solo se puede empezar una sesión si aún no hay personaje cargado: para cambiar de personaje hay que hacer `quit`.
- **Solo se guarda al salir** (`quit`/`exit`, o cuando se cierra la entrada: Ctrl+Z / fin de un pipe). Nunca a mitad de sesión. Ctrl+C no guarda.
- **`CharacterSave`** (`src/World/CharacterSave.cs`) es un DTO plano, separado de `Combatant` (que lleva estado de ejecución: cooldowns, buffs, estado de combate). Guarda nombre, **raza por id**, **profesión por id**, **nivel**, **habitación por id (`roomId`)**, HP/MP/gold/experience actuales y máximos, las 9 stats/skills. Lleva `Version` para poder migrar el formato. `CharacterSave.From(combatant, roomId)` y `ToCombatant(findRace, findProfession, startingWeapon)` hacen la conversión. **No se guardan** cooldowns, buffs ni los emotes personalizados del arma.
  - **DECIDIDO: el equipo NO se persiste, solo el gold.** Arma, armas envainadas, armadura puesta/en mano, mochila y su contenido **se pierden al hacer `quit`** salvo que se hayan vendido antes. Motivo: este prototipo no tiene un "servidor" que siga corriendo entre sesiones — `quit` termina el proceso entero, y con él todo el `WorldState` en memoria (cadáveres, items en el suelo, todo). Implementar "se queda tirado en el suelo de la habitación para la próxima vez" exigiría persistir el mundo entero a disco, no solo lo del jugador — se decidió que perderlo es la alternativa simple y honesta. `ToCombatant` rearma al jugador con `Fists` (vía `CharacterLoader.CreateStarterWeapon()`, siempre de `data/player.json`), igual que un personaje nuevo. `SaveCharacter` avisa en pantalla si te vas dejando algo sin vender.
- Un personaje **caído (sangrando o muerto) se guarda como si lo hubieran vendado**: vivo, con el 10% del HP máximo (`BandageHealFraction`).
- `ICharacterStore` (`Exists`/`Load`/`Save`) es la interfaz de almacenamiento; el prototipo usa **`JsonCharacterStore`**: un JSON por personaje en `%APPDATA%\RpgGr\saves\<nombre>.json` (fuera del repo y del `bin`, para que un rebuild o un clean no borre progreso). Escritura en fichero temporal + reemplazo. Más adelante se cambiará por EF Core/SQL en el servidor sin tocar lo demás.
- **Bug corregido (`continue` con saves antiguos):** al añadir `ProfessionId` (y antes `Level`) a `CharacterSave`, los personajes guardados **antes** de esos campos existir no tenían esas claves en su JSON; como `ProfessionId` era `required`, `JsonSerializer` lanzaba y `continue` reventaba el proceso entero. Arreglado por dos lados: `ProfessionId` ya no es `required` (default `"civilian"`, igual que `Level` ya defaulteaba a 1), y `JsonCharacterStore.Load` ahora atrapa `JsonException` y devuelve `null` en vez de dejar que cualquier futuro cambio de formato tumbe el programa — para eso estaba pensado `CharacterSave.Version`, que ya existía pero no se usaba. `HandleContinue` distingue con `store.Exists` entre "no existe ese personaje" y "existe el fichero pero no se pudo leer". **No hace falta recrear personajes viejos**: cargan con profesión `civilian`/nivel 1/0 XP por defecto, conservando el gold guardado.
- Los nombres son solo letras (también evita que la entrada escape de la carpeta) y **únicos sin distinguir mayúsculas**: `create` rechaza uno ya guardado. Si la habitación guardada ya no existe, se entra por la habitación inicial.
- Las stats base de un personaje nuevo salen de `data/player.json`; a partir de ahí el personaje guardado lleva sus propios valores (que crecerán con el entrenamiento).


### Razas (DECIDIDO)
- Cada personaje (player y NPC) tiene una **raza** (`"race": "<id>"` en su JSON → `data/races/<id>.json`, clase `Race` en `src/Combat/Race.cs`). Si el id no existe, la carga falla con un error claro.
- La raza da **bonos/penalizaciones en %** sobre los stats base (`X * (1 + pct/100)`), **solo a los stats, nunca a las 4 skills** (las skills se entrenan). Se aplica en `Combatant.EffectiveStat`, antes de los buffs planos y antes del tope de 125.
- Existe un 5º stat, **`Intelligence`**: no interviene en ninguna fórmula de combate (a propósito), se usará en las habilidades de profesión. Es `required` en el JSON y está en `StatType` (los buffs pueden modificarlo).
- **Cada raza suma 0** (ninguna es mejor que otra):

| Raza | Bonos | Penalizaciones |
|---|---|---|
| human | +5% Coordination | -5% Intelligence |
| dwarf | +10% Constitution, +10% Strength | -10% Intelligence, -5% Coordination, -5% Agility |
| elf | +5% Agility, +10% Intelligence | -10% Constitution, -5% Strength |
| orc | +10% Strength, +5% Constitution | -10% Intelligence, -5% Coordination |
| creature | — (sin bonos, raza por defecto de NPCs como rat/deer/beast) | — |

- El player actual es `human` (placeholder, aún no hay creación de personaje). Ojo: con stats base ~20, un 10% son solo +2 puntos; el efecto crece con stats altos.

### Niveles, entrenamiento, profesiones y títulos (DECIDIDO e implementado)
- **Subir de nivel exige DOS cosas a la vez**: experiencia suficiente **y** un promedio suficiente de los **5 stats base** (Strength/Constitution/Agility/Coordination/Intelligence — las 4 skills NO cuentan, esas se entrenan aparte y son propias de cada profesión). El promedio usa los stats **base** (sin raza ni buffs) — así un buff temporal no puede "destrabar" un nivel que luego se pierde al pasar el buff.
  - XP: nivel 2 = 500, cada nivel siguiente = el anterior × 1.33 (`Leveling.ExperienceRequired`).
  - Promedio de stats: 25 en nivel 2, +5 por nivel (`15 + 5×nivel`, `Leveling.RequiredStatAverage`) — nivel 3=30, nivel 4=35, nivel 5=40, etc.
  - **Ya NO sube de nivel solo (cambiado):** `AddExperience` solo acumula XP, no revisa nada. Subir de nivel es una acción manual, `Combatant.TryLevelUp(out error)`, y **solo funciona en presencia de un entrenador** (comando `levelup`, exige `Room.Trainer != null`; el chequeo de "estar en la sala del entrenador" es cosa de `Program.cs`, `Combat` no sabe de habitaciones). Sube **un nivel por llamada** — si tienes XP para varios de golpe, hay que llamar `levelup` varias veces.
  - **Niveles redondos (5, 10, 15, 20) bloquean el avance** más allá de ellos hasta completar una "tarea especial" — sistema de tareas **no implementado todavía**, así que por ahora es un muro duro: no se puede pasar de nivel 5 a 6 (ni 10→11, etc.) de ninguna forma. `Combatant.TryLevelUp` lo rechaza con un mensaje explícito.
  - `Combatant.Level` (arranca en 1), evento `OnLevelUp`. Al cargar un save, `RestoreLevel` fija el nivel guardado **sin** re-derivarlo de la XP (el nivel persistido es la fuente de verdad, para que un cambio futuro de fórmula no re-nivele personajes viejos).
- **Experiencia (recordatorio, ya implementado antes):** cada golpe del jugador da XP = daño hecho; matar da un bono plano = HP máximo del NPC. `Combatant.Experience`, se persiste.
- **Entrenamiento de stats/skills (DECIDIDO e implementado):** `src/Combat/Training.cs`. Cuesta gold por punto, **1-10 = 10 gold/punto**, y cada bracket de 10 siguiente ×1.33 el precio del anterior:

  | Bracket | Puntos | Gold/punto | Acumulado desde 0 |
  |---|---|---|---|
  | 0 | 1-10 | 10 | 100 |
  | 1 | 11-20 | 13 | 230 |
  | 2 | 21-30 | 18 | 410 |
  | 3 | 31-40 | 24 | 650 |
  | 4 | 41-50 | 31 | 960 |
  | 5 | 51-60 | 42 | 1380 |
  | 6 | 61-70 | 55 | 1930 |
  | 7 | 71-80 | 74 | 2670 |
  | 8 | 81-90 | 98 | 3650 |
  | 9 | 91-100 | 130 | 4950 |

  Números tal cual los dio el desarrollador; **ya avisó que probablemente sea muy agresivo al final** (130 gold/punto, ~5000 gold de 0 a 100) y se balanceará después.
  - **Multiplicador ×1.33 confirmado por ahora.** Se evaluó subirlo a ×1.66 (llevaría el 0→100 de ~4,950 a ~23,930 gold, ×4.8 más caro solo en los últimos brackets) pero se descarta mientras la única fuente de gold sean rat/deer/beast. **Revisar cuando existan NPCs más poderosos que suelten oro/botín mucho más caro** — ahí sí tendría sentido subir el multiplicador para que el entrenamiento de tope siga sintiéndose caro en relación a la nueva economía.
  - **Tope por nivel:** ningún stat ni skill se puede entrenar por encima de `30 + 5×nivel` (`Training.MaxTrainableValue`) — 3 niveles de margen por encima de lo que exigiría el nivel actual. A nivel 1: tope 35 (ej. del desarrollador, confirmado).
  - Se entrena en **`src/Combat/Combatant.cs`**: los 9 stats/skills base pasaron de `init` a mutables (siguen siendo `required` para la carga JSON/save, solo que ahora también se pueden cambiar después). `Combatant.TryTrain(stat, puntos, out error)` cobra y sube; `TrainingCap`/`TrainingCostFor`/`BaseStat` para consultar sin gastar.
- **Entrenador (DECIDIDO e implementado):** `World.Trainer` (paralelo a `Shop`/`Healer`), en **Millford Training Yard** (abajo, `down`, desde la plaza). Entrena los 9 stats/skills genéricos (no hay skills propias de profesión todavía) y es quien aplica el `levelup`. `Trainer.ProfessionId` existe para cuando haya entrenadores por profesión (a futuro, cuando dejen de ser todos `civilian`), pero no se usa todavía. `look` al entrenador (o `look trainer`), o el comando `train` sin argumentos, muestran el menú completo (valor actual, costo del siguiente punto, tope); `train <stat> [cantidad]` (cantidad por defecto 1) entrena de verdad.
- **Comando `score` (DECIDIDO e implementado):** muestra la ficha completa del jugador — nombre/raza/título/nivel, XP (y cuánta falta para el siguiente nivel), HP/MP/Gold, y los 9 stats/skills base.
- **Profesiones (DECIDIDO e implementado, sin skills de profesión todavía):** `Profession` (`src/Combat/Profession.cs`, `data/professions/<id>.json`), mismo patrón que `Race`. Por ahora **todos son `civilian`** (`data/professions/civilian.json`). Una profesión no afecta el nivel (eso depende solo de XP + stats) — **solo determina el título** del personaje según su nivel.
  - `Profession.Titles`: lista de brackets `{minLevel, maxLevel, title}`. `Combatant.Title` = el bracket que matchea el nivel actual, o el nombre plano de la profesión si no hay bracket (ej. nivel 21+, sin bracket definido).
  - **Títulos de `civilian` (placeholders genéricos, pendientes de que el desarrollador ponga los definitivos):** 1-4 *Villager*, 5-9 *Freeholder*, 10-14 *Elder*, 15-19 *Notable*, 20 *Luminary*. **Más de 20: sin bracket** — los títulos ahí se piensan personalizados por personaje (no implementado), así que cae al nombre plano `"Civilian"`.
  - **Mismos títulos para todas las razas por ahora** — a futuro se plantea dividir por "tipo de raza" (ej. orcos vs. elfos/humanos/enanos no comparten título), el esquema ya lo permite sin rediseño (solo hay que ampliar `Profession`/los JSON cuando toque).
  - Formato de display: `"{Nombre} the {raza} {título}"` (ej. *"Pepe the orc Villager"*, calcando el ejemplo `"Pepe the orc marauder of the horde"` que dio el desarrollador). Se usa en `create`, `look me`, `score` y el anuncio de subida de nivel.

### Fórmulas (DECIDIDAS)
```
// Acierto
offenseAcc = Coordination + Aim + weapon.Hit                (atacante)
defenseAcc = Agility + Dodge + armor.TotalDeflect            (defensor)
hitChance  = offenseAcc / (offenseAcc + defenseAcc)          // 0.5 si offense == defense

// Crítico (si no hubo miss)
critChance = 0.5% + weapon.CritChanceBonus

// Daño no-crítico: SIEMPRE 1-30, con variación aleatoria (rule 6.3): jitter normal ±20% (80% de los golpes),
// 15% de golpes "flojos" (jitter 0-0.8, WeakHitChance) y 5% "fuertes" (jitter 1.2-1.6, StrongHitChance),
// para que los tiers bajos sigan siendo posibles con techos altos y a veces se llegue un tier por encima del techo.
// OJO: NO es un ratio puro ofensa/defensa (eso hacía que dos personajes igual de
// débiles se dieran golpes de Massacre igual que dos personajes igual de fuertes).
// La ofensa absoluta del atacante primero pone el techo; la defensa del rival
// solo mitiga una fracción de ese techo, sobre la misma escala de referencia.
offenseDmg = Strength + Attack + weapon.Damage               (atacante)
defenseDmg = Constitution + Defense + armor.TotalAbsorb      (defensor)
REF        = 300   // 100 Strength + 100 Attack + 100 weapon.Damage, el techo "entrenado a tope" sin buffs
potential  = clamp(offenseDmg / REF * 30, 1, 30)             // el techo del atacante, sin importar el rival
mitigation = defenseDmg / (defenseDmg + REF)                 // el rival solo recorta una fracción de ese techo
damage     = clamp(round(potential * (1 - mitigation) * jitter), 1, 30)   // jitter: ver arriba (80% [0.8,1.2], 15% [0,0.8), 5% [1.2,1.6))

// Daño crítico: SIEMPRE 31-50
// ~90% de las veces se agrupa alrededor de weapon.CritPower (±15%);
// ~10% de las veces ignora el arma y sale un valor totalmente aleatorio,
// para que hasta un arma con CritPower alto pueda sacar alguna vez un 31.
damage = 31 + round(effectivePos * 19)   // effectivePos en [0,1], ver AttackResolver.cs
```
- **Niveles de golpe fijos por rango de daño** (no por posición relativa ni por % de vida — se descarta la opción (b) de "Pendiente de decidir"): Graze 1-3, Hit 4-7, Hit Hard 8-10, Hit Very Hard 11-14, Massive Damage 15-19, Massacre 20-30, Critical 31-50.
- Implementado en `src/Combat/AttackResolver.cs` (fórmula única, reutilizada tanto por ataques normales como por habilidades vía bonus).

## Pendiente de decidir
- Lista de profesiones y sus habilidades.
- Si el NPC debe perseguir al jugador tras una huida, o cualquier otra consecuencia más allá de "sin penalización" (por ahora huir es gratis y no hay persecución).
- Ajustar los valores concretos de stats/armas/armaduras de ejemplo: con los actuales el jugador gana demasiado (~93% en simulación).

## Siguiente paso
Montar un **prototipo de consola en C# (.NET 8)** con: rondas automáticas, iniciativa de quien usa `kill`, tirada de acierto/fallo, daño aleatorio, 6 niveles + crítico con los emotes de arriba, emotes personalizados por arma y al menos una habilidad con cooldown en turnos. Objetivo: ver cómo se siente un combate y equilibrar antes de pasarlo a Unity.

## Estado actual del prototipo
- Solución `RpgGr.sln` con tres proyectos (**net9.0** — SDK disponible es .NET 9, no .NET 8):
  - `src/Combat`: motor de combate en C# puro, sin dependencias de Unity (`CombatEngine`, `Combatant`, `Weapon`, `Armor`, `Skill`, `StatType`/`StatModifier`, `AttackResolver`, `EmoteTable`, `IRandomSource` inyectable con semilla).
  - `src/World`: habitaciones, áreas y estado del mundo (ver "Mundo y movimiento"), también en C# puro.
  - `src/ConsoleProto`: prototipo de consola, ahora un **bucle de comandos** real (`create`, `continue`, `look [target]`, movimiento `n/s/e/w/u/d`, `kill <target>`, `shape [target]`, `bandage [target]`, `flee`/`stop`, `wield`/`sheath`/`wear`/`remove`, `hands`, `i`/`inventory`, `gold`, `xp`, `score`, `take`/`get`/`loot`, `list`/`buy`/`sell`, `heal`, `train`/`levelup`, `repair`/`condition`, `simulate [rounds] [npc]`, `reset`, `listk`, `quit`), no una pelea guionizada. `CharacterLoader` deserializa cada personaje directamente en un `Combatant` vía `System.Text.Json` (sus propiedades `required` obligan a que el JSON tenga todas las stats).
- **`create`**: crea el player (`CharacterCreation.cs`): pregunta el nombre (solo letras, 2-16, se capitaliza; vacío = cancelar), muestra la lista de razas jugables con una **descripción narrativa** (campo `description` del JSON de la raza, con pistas de fortalezas/debilidades **sin números**) y pide escribir el id exacto (sin distinguir mayúsculas; vacío = cancelar); luego imprime un texto de creación. Stats base salen de `data/player.json` (con el nombre y la raza elegidos). Se guarda en `quit` (ver "Persistencia del personaje"). Sin personaje, `kill`/`look`/`simulate`/`reset` piden hacer `create` o `continue` primero. Los % exactos solo viven en los JSON/CLAUDE.md, no se enseñan al jugador. Las razas con `"playable": false` (creature) no salen en la lista.
- **Prompt de entrada (`LineEditor.cs`)**: la consola lee tecla a tecla para que los mensajes del combate (que llegan desde otro hilo) no partan por la mitad lo que el usuario está escribiendo: se borra la línea, se imprime el mensaje y se redibuja `> ` + lo escrito. Soporta escribir, backspace y historial con ↑/↓ (no mover el cursor dentro de la línea). Si la entrada está redirigida (tests con pipe) usa `Console.ReadLine`. Todo texto impreso desde el bucle de combate debe pasar por `LineEditor.Print`.
- **El HP ajeno nunca se muestra en número** (rondas, `look`, etc.). El comando `shape` (sin argumento = tu oponente actual en combate; `shape <nombre>` = ese objetivo si coincide con el NPC activo) da una de 6 pistas según el % de vida, de peor a mejor: *critical condition, very close to death* / *near death* / *doesn't look so great* / *average condition* / *good condition* / *perfect condition* (más un mensaje aparte si está muerto). Implementado en `Program.cs` (`DescribeCondition`), es puramente de consola — la librería `Combat` sigue exponiendo `CurrentHp`/`MaxHp` normales, la ofuscación es solo de presentación.
  - **Excepción: tu propio HP/MP sí se ve, en el prompt.** Una vez hay personaje cargado, el prompt pasa de `> ` a `HP: <actual>  MP: <actual> > ` (valor actual, no `/máximo`). Es un caso aparte de la regla de arriba — sobre ti mismo sí conoces el número exacto en todo momento; la ofuscación sigue aplicando a cómo ves a los demás.
  - **El prompt se refresca solo, en vivo (bug corregido):** `LineEditor.ReadLine` recibía el prompt como `string` fijo, calculado una sola vez al empezar a leer — un golpe recibido a mitad de combate imprimía el emote pero el `HP: x MP: y >` seguía mostrando los valores de antes de la pelea hasta darle Enter. Ahora recibe un `Func<string>` y lo reevalúa en cada redibujado (`Print`/`Redraw`/`Replace`), así que cualquier mensaje que llega desde el bucle de combate (justo cuando cambia el HP) refresca también el prompt en el mismo momento.
- Datos en **JSON** (no BD todavía; los personajes guardados van en `%APPDATA%\RpgGr\saves`): `src/ConsoleProto/data/player.json` y `data/npcs/{rat,deer,beast}.json`, copiados a la salida de compilación. Personajes de nivel 1:
  - Player (raza human): 20 en las 4 stats y en Intelligence, 0 en las 4 skills (arrancan en 0 y se entrenan más adelante — no implementado), 50 HP, 0 MP (reservado para skills/hechizos futuros, sin uso aún), sin arma ni armadura.
  - Rat/Deer/Beast: 5/15/25 en las **4 stats**, **0 en las 4 skills** (igual que el player, ver nota más abajo), 25/45/60 HP, con un "arma natural" (Bite/Antlers/Claws) cuyos stats de combate están todos en 0 — solo aporta el nombre para emotes futuros.
- Implementado: rondas automáticas con turnos de 1s/ronda de 2s (`PlayFirstTurn`/`PlaySecondTurn`, punto 8bis), `flee` sin penalización que respeta la ronda en curso (`RequestFlee`, punto 8ter), iniciativa fija de quien inicia el combate, sistema completo de stats/skills/arma/armadura/buffs, clasificación en los 6 niveles + crítico por rango fijo de daño, emotes estándar con variantes.
- Nota de implementación: la perspectiva "You" de los emotes depende de `Combatant.IsPlayer`, no de quién tiene la iniciativa — un NPC agresivo puede iniciar el combate (tener la iniciativa) sin dejar de aparecer en tercera persona en los emotes.
- **Bug corregido (skills de NPCs):** al principio les había puesto a rat/deer/beast su valor parejo en las 8 stats+skills, mientras que el player (por diseño) tiene las skills en 0 hasta entrenarlas. Como las skills pesan igual que los stats en las fórmulas, el deer (15 en todo) le ganaba en acierto y daño a un player de stats 20 pero skills 0 — justo al revés de lo esperado. Ahora los NPCs también tienen las 4 skills en 0; solo sus 4 stats escalan con el "tier" de la criatura.
- Supuestos tomados sin confirmar explícitamente (fáciles de cambiar, avisar si no son correctos):
  - `weapon.Hit` en escala 0-100, igual que `weapon.Damage`, por simetría.
  - `Absorb` y `Deflect` de la armadura son dos "bolsas" independientes (cada una con tope 100 sumando todas las piezas), no un total combinado.
  - Crítico "flojo" aleatorio: 10% de probabilidad de ignorar `CritPower` del arma y salir un valor totalmente al azar en 31-50; el otro 90% se agrupa ±15% alrededor de la posición que marca `CritPower`.
  - MP del jugador puesto a 0 (placeholder honesto, no un número inventado) hasta que se diseñen las habilidades que lo consuman.
- Probado a mano: `kill rat` (el jugador gana fácil, la rata casi no acierta), `flee` a mitad de combate (la ronda en curso se completa, luego se detiene), `reset` (se niega si hay combate activo), `kill deer` (con las skills del jugador en 0 frente a las del deer en 15, el jugador puede perder — señal de que un personaje "sin entrenar" es débil de verdad, tal como se diseñó).
- **`simulate [rounds] [npc]`** (por defecto 1000 rondas, todos los NPC) es una herramienta de **diagnóstico**, en `src/ConsoleProto/Simulation.cs`: por cada NPC (todos los `.json` de `data/npcs/`, detectados con `CharacterLoader.ListNpcIds()`, o solo el indicado) resuelve N rondas de un ataque por lado con `AttackResolver.Resolve` directamente (sin `CombatEngine`, sin delays), **ignorando el HP** (nadie muere). Imprime, por Player/NPC: cantidad de golpes por tipo (Miss, Graze… Critical), hit rate, daño medio por golpe y por ronda. Hallazgo con los valores actuales: casi todo es `Graze` (techo de daño del player = 40/300×30 = 4), sin `Hit`+ salvo algún crítico; hacen falta arma/skills para niveles altos.
- Se quitó, por no encajar todavía con este flujo dirigido por datos: la pelea guionizada anterior (jugador vs. orco con stats hardcodeadas) y la skill de ejemplo `Power Strike`. Se pueden recuperar/adaptar cuando volvamos a habilidades.
