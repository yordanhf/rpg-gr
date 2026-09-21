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
5. **Fin del combate:** muere uno de los dos, o el jugador **se aleja** del NPC (fuera de rango). En Unity se comprueba la distancia en cada ronda.
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
8. Al final de cada ronda: bajan los cooldowns y avanza la duración de buffs y debuffs.

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

## Pendiente de decidir
- **Cómo se asigna el nivel del golpe:**
  - (a) por la **posición de la tirada dentro del rango de daño posible** (propuesto en el esqueleto; los emotes siguen teniendo sentido al subir de nivel), o
  - (b) por el **% de vida del objetivo** que quita el golpe (más dramático).
- Fórmulas concretas de acierto, crítico, daño y armadura, y qué stats existen.
- Intervalo de tiempo entre rondas.
- Qué pasa al huir (¿penalización?, ¿el NPC te persigue?).
- Lista de profesiones y sus habilidades.

## Siguiente paso
Montar un **prototipo de consola en C# (.NET 8)** con: rondas automáticas, iniciativa de quien usa `kill`, tirada de acierto/fallo, daño aleatorio, 6 niveles + crítico con los emotes de arriba, emotes personalizados por arma y al menos una habilidad con cooldown en turnos. Objetivo: ver cómo se siente un combate y equilibrar antes de pasarlo a Unity.

## Estado actual del prototipo
- Solución `RpgGr.sln` con dos proyectos (**net9.0** — SDK disponible es .NET 9, no .NET 8):
  - `src/Combat`: motor de combate en C# puro, sin dependencias de Unity (`CombatEngine`, `Combatant`, `Weapon`, `Skill`, `EmoteTable`, `IRandomSource` inyectable con semilla).
  - `src/ConsoleProto`: prototipo de consola. Modo interactivo (`dotnet run --project src/ConsoleProto`) y modo de simulación masiva (`dotnet run --project src/ConsoleProto -- simulate <N>`) para equilibrar sin jugar combate a combate.
- Implementado: rondas automáticas, iniciativa fija de quien inicia el combate, acierto/fallo, crítico, daño con variación ±20%, clasificación en los 6 niveles + crítico, emotes estándar con variantes, emotes personalizados por arma (ejemplo en la espada del jugador para crítico), una habilidad de ejemplo (`Power Strike`) con cooldown en turnos que se encola y sustituye al ataque normal.
- Decisión tomada para el prototipo sobre el punto pendiente "cómo se asigna el nivel del golpe": opción **(a)**, por posición de la tirada dentro del rango de daño posible (la que ya proponía el esqueleto de referencia). La opción (b) por % de vida restante sigue sin descartarse si (a) no convence al probarlo.
- Nota de implementación: la perspectiva "You" de los emotes depende de `Combatant.IsPlayer`, no de quién tiene la iniciativa — un NPC agresivo puede iniciar el combate (tener la iniciativa) sin dejar de aparecer en tercera persona en los emotes.
- Con las stats de ejemplo (jugador vs. orco) la simulación masiva da ~96% de victorias del jugador en ~7 rondas de media: demasiado favorable, pendiente de ajustar cuando se definan fórmulas y stats reales.
- Sigue pendiente todo lo de la sección "Pendiente de decidir" salvo el punto de nivel de golpe ya decidido arriba para el prototipo.
