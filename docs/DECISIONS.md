# HwSync Architecture Decisions


## ADR-001 — .NET version

**Status:** Accepted  
**Date:** 2026-08-09

### Decision

HwSync uses .NET 10 LTS as its primary target framework.

### Rationale

HwSync is a new project and is also intended as a practical environment
for learning modern .NET development.

There is no requirement to maintain compatibility with legacy .NET versions.

All new HwSync projects should target:

`net10.0`


## ADR-002: Method parameter convention

**Status:** Accepted

### Decision

HwSync methods should normally have no more than two semantic input parameters.

If a method requires more than two logically related input values, those values
should normally be grouped into a dedicated parameter object.

Example:

```csharp
Task ScanAsync(
    ScanParameters parameters,
    CancellationToken cancellationToken);
```

### Технические параметры

Технические параметры метода не учитываются при применении правила
«не более двух смысловых параметров».

К таким параметрам относятся, например:

- `CancellationToken`;
- параметры, необходимые инфраструктуре или механизму выполнения метода,
  но не являющиеся частью его бизнес-входных данных.

Например:

```csharp
Task ScanAsync(
    ScanParameters parameters,
    CancellationToken cancellationToken);
```

В данном случае метод имеет один смысловой параметр — `ScanParameters`.
`CancellationToken` в ограничение количества смысловых параметров не входит.

### Конструкторы

Правило «не более двух смысловых параметров» не распространяется
на конструкторы.

В частности, класс может получать через Dependency Injection несколько
явно указанных зависимостей:

```csharp
public SyncService(
    IFileSnapshotProvider snapshotProvider,
    IFileStateStore stateStore,
    ILogger<SyncService> logger)
{
}
```

Не следует объединять зависимости конструктора в искусственный объект
параметров только ради соблюдения правила двух параметров.

### Причина

Ограничение количества смысловых параметров предназначено для того,
чтобы сигнатуры методов оставались понятными, а логически связанные
входные данные группировались в отдельные классы.

Технические параметры имеют другое назначение и не должны влиять
на это правило.

Зависимости конструктора также должны оставаться явно видимыми,
особенно при использовании Dependency Injection.	
		
		
			
		
## ADR-003: Явные типы локальных переменных

**Статус:** Принято

### Решение

В HwSync для локальных переменных используем явное указание типа
вместо `var`.

Предпочтительно:

```csharp
ChangeComparer comparer = new();

IReadOnlyCollection<FileChange> changes =
    comparer.Compare(previous, current);

FileChange change = changes.Single();
```

Не используем без необходимости:

```csharp
var comparer = new ChangeComparer();
var changes = comparer.Compare(previous, current);
var change = changes.Single();
```

Если тип уже однозначно указан слева, используем сокращённую форму `new()`:

```csharp
ChangeComparer comparer = new();
List<FileChange> changes = new();
```

вместо избыточного повторения:

```csharp
ChangeComparer comparer = new ChangeComparer();
List<FileChange> changes = new List<FileChange>();
```

### Причина

Явное указание типа позволяет сразу видеть тип локальной переменной
без необходимости полагаться на вывод типа компилятором или средства IDE.

Использование `new()` при явно указанном типе слева позволяет при этом
не дублировать одну и ту же информацию.


## ADR-004: Использование современных возможностей C#

**Статус:** Принято

### Решение

HwSync разрабатывается на .NET 10, поэтому современные возможности C#
используем там, где они делают код проще и понятнее.

В частности:

- collection expressions `[]`;
- target-typed `new()`;
- `record` для подходящих моделей;
- pattern matching;
- другие современные конструкции C#, если они улучшают читаемость.

Новые возможности языка не должны использоваться только потому,
что они существуют.

Читаемость и понятность кода имеют приоритет.

### Причина

HwSync в том числе является проектом для изучения современных
возможностей .NET и C#.

При этом использование новых возможностей языка должно приносить
практическую пользу: уменьшать лишний код, повышать выразительность
или упрощать дальнейшее сопровождение.


