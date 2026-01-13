# 函数调用（Function Calling）

> 原文链接: https://platform.openai.com/docs/guides/function-calling

为模型提供新的功能和数据访问能力，使其能够按照指令响应提示。

函数调用（也称为工具调用）为 OpenAI 模型提供了一种强大而灵活的方式，使其能够与外部系统交互并访问训练数据之外的数据。本指南展示了如何将模型连接到你的应用程序提供的数据和操作。我们将展示如何使用函数工具（由 JSON Schema 定义）和自定义工具（使用自由格式文本输入和输出）。

---

## 工作原理

让我们首先了解一些关于工具调用的关键术语。在我们对工具调用有共同的词汇后，我们将通过一些实际示例展示它是如何完成的。

- **工具（Tools）** - 我们提供给模型的功能
- **工具调用（Tool calls）** - 模型发出的使用工具的请求
- **工具调用输出（Tool call outputs）** - 我们为模型生成的输出

### 函数与工具

"函数"和"工具"这两个术语在 LLM API 的上下文中经常互换使用。在 OpenAI 的 API 中，"函数"是模型可以调用的特定类型的"工具"。

### 工具调用流程

工具调用是你的应用程序和模型之间通过 OpenAI API 进行的多步骤对话。工具调用流程有五个高级步骤：

1. **向模型发送请求，并附带它可以调用的工具**
2. **从模型接收工具调用**
3. **在应用程序端使用工具调用的输入执行代码**
4. **向模型发送第二个请求，附带工具输出**
5. **从模型接收最终响应（或更多工具调用）**

---

## 函数工具示例

让我们看一个 `get_horoscope` 函数的端到端工具调用流程，该函数获取某个星座的每日运势。

### 完整的工具调用示例（Python）

```python
from openai import OpenAI
import json

client = OpenAI()

# 1. 为模型定义可调用工具列表
tools = [
    {
        "type": "function",
        "name": "get_horoscope",
        "description": "获取某个星座的今日运势。",
        "parameters": {
            "type": "object",
            "properties": {
                "sign": {
                    "type": "string",
                    "description": "星座名称，如金牛座或水瓶座",
                },
            },
            "required": ["sign"],
        },
    },
]

def get_horoscope(sign):
    return f"{sign}: 下周二你将结交一只小水獭朋友。"

# 创建一个运行中的输入列表，我们会随时间添加内容
input_list = [
    {"role": "user", "content": "我的运势是什么？我是水瓶座。"}
]

# 2. 使用定义的工具提示模型
response = client.responses.create(
    model="gpt-4",
    tools=tools,
    input=input_list,
)

# 保存函数调用输出以供后续请求使用
input_list += response.output

for item in response.output:
    if item.type == "function_call":
        if item.name == "get_horoscope":
            # 3. 执行 get_horoscope 的函数逻辑
            horoscope = get_horoscope(json.loads(item.arguments))
            
            # 4. 向模型提供函数调用结果
            input_list.append({
                "type": "function_call_output",
                "call_id": item.call_id,
                "output": json.dumps({
                  "horoscope": horoscope
                })
            })

print("最终输入:")
print(input_list)

response = client.responses.create(
    model="gpt-4",
    instructions="只使用工具生成的运势进行响应。",
    tools=tools,
    input=input_list,
)

# 5. 模型应该能够给出响应！
print("最终输出:")
print(response.model_dump_json(indent=2))
print("\n" + response.output_text)
```

请注意，对于像 GPT-5 或 o4-mini 这样的推理模型，模型响应中带有工具调用返回的任何推理项也必须与工具调用输出一起传回。

---

## 定义函数

函数可以在每个 API 请求的 `tools` 参数中设置。函数由其模式定义，该模式告知模型它的功能和期望的输入参数。函数定义具有以下属性：

| 属性 | 描述 |
|------|------|
| type | 应始终为 `function` |
| name | 函数名称（例如 `get_weather`） |
| description | 何时以及如何使用该函数的详细信息 |
| parameters | 定义函数输入参数的 JSON Schema |
| strict | 是否对函数调用强制执行严格模式 |

### 函数定义示例

```json
{
    "type": "function",
    "name": "get_weather",
    "description": "获取给定位置的当前天气。",
    "parameters": {
        "type": "object",
        "properties": {
            "location": {
                "type": "string",
                "description": "城市和国家，例如：北京，中国"
            },
            "units": {
                "type": "string",
                "enum": ["celsius", "fahrenheit"],
                "description": "返回温度的单位。"
            }
        },
        "required": ["location", "units"],
        "additionalProperties": false
    },
    "strict": true
}
```

因为 `parameters` 由 JSON Schema 定义，你可以利用其丰富的特性，如属性类型、枚举、描述、嵌套对象和递归对象。

### 定义函数的最佳实践

1. **编写清晰详细的函数名称、参数描述和说明。**
   - 明确描述函数的目的和每个参数（及其格式），以及输出代表什么。
   - 使用系统提示描述何时（以及何时不）使用每个函数。通常，准确告诉模型该做什么。
   - 包含示例和边缘情况，特别是用于纠正任何重复出现的失败。（注意：添加示例可能会影响推理模型的性能。）

2. **应用软件工程最佳实践。**
   - 使函数显而易见且直观。（最小惊讶原则）
   - 使用枚举和对象结构使无效状态不可表示。（例如 `toggle_light(on: bool, off: bool)` 允许无效调用）
   - 通过实习生测试。实习生/人类能否仅根据你给模型的内容正确使用该函数？（如果不能，他们会问你什么问题？将答案添加到提示中。）

3. **从模型卸载负担，尽可能使用代码。**
   - 不要让模型填写你已经知道的参数。例如，如果你已经基于之前的菜单有了 `order_id`，不要有 `order_id` 参数——相反，使用无参数的 `submit_refund()` 并用代码传递 `order_id`。
   - 合并总是按顺序调用的函数。例如，如果你总是在 `query_location()` 之后调用 `mark_location()`，只需将标记逻辑移入查询函数调用中。

4. **保持函数数量少以提高准确性。**
   - 用不同数量的函数评估你的性能。
   - 目标是在任何时候少于 20 个函数，尽管这只是一个软性建议。

5. **利用 OpenAI 资源。**
   - 在 Playground 中生成和迭代函数模式。
   - 考虑使用微调来提高大量函数或困难任务的函数调用准确性。

### Token 使用

在底层，函数以模型训练过的语法注入到系统消息中。这意味着函数计入模型的上下文限制，并作为输入 token 计费。如果你遇到 token 限制，我们建议限制函数数量或你为函数参数提供的描述长度。

如果你的工具规范中定义了许多函数，也可以使用微调来减少使用的 token 数量。

---

## 处理函数调用

当模型调用函数时，你必须执行它并返回结果。由于模型响应可以包含零个、一个或多个调用，最佳实践是假设有多个。

响应 `output` 数组包含一个 `type` 值为 `function_call` 的条目。每个条目带有 `call_id`（稍后用于提交函数结果）、`name` 和 JSON 编码的 `arguments`。

### 带有多个函数调用的示例响应

```json
[
    {
        "id": "fc_12345xyz",
        "call_id": "call_12345xyz",
        "type": "function_call",
        "name": "get_weather",
        "arguments": "{\"location\":\"巴黎, 法国\"}"
    },
    {
        "id": "fc_67890abc",
        "call_id": "call_67890abc",
        "type": "function_call",
        "name": "get_weather",
        "arguments": "{\"location\":\"波哥大, 哥伦比亚\"}"
    },
    {
        "id": "fc_99999def",
        "call_id": "call_99999def",
        "type": "function_call",
        "name": "send_email",
        "arguments": "{\"to\":\"bob@email.com\",\"body\":\"你好 bob\"}"
    }
]
```

### 执行函数调用并附加结果（Python）

```python
for tool_call in response.output:
    if tool_call.type != "function_call":
        continue

    name = tool_call.name
    args = json.loads(tool_call.arguments)

    result = call_function(name, args)
    input_messages.append({
        "type": "function_call_output",
        "call_id": tool_call.call_id,
        "output": str(result)
    })
```

在上面的示例中，我们有一个假设的 `call_function` 来路由每个调用。这是一个可能的实现：

```python
def call_function(name, args):
    if name == "get_weather":
        return get_weather(**args)
    if name == "send_email":
        return send_email(**args)
```

### 格式化结果

你在 `function_call_output` 消息中传递的结果通常应该是一个字符串，格式由你决定（JSON、错误代码、纯文本等）。模型将根据需要解释该字符串。

对于返回图像或文件的函数，你可以传递图像或文件对象的数组而不是字符串。

如果你的函数没有返回值（例如 `send_email`），只需返回一个指示成功或失败的字符串。（例如 `"success"`）

### 将结果合并到响应中

将结果附加到你的 `input` 后，你可以将它们发送回模型以获得最终响应。

```python
response = client.responses.create(
    model="gpt-4.1",
    input=input_messages,
    tools=tools,
)
```

**最终响应：** `"巴黎大约15°C，波哥大18°C，我已经给Bob发送了那封邮件。"`

---

## 其他配置

### 工具选择

默认情况下，模型将决定何时以及使用多少工具。你可以使用 `tool_choice` 参数强制特定行为。

1. **Auto：** （默认）调用零个、一个或多个函数。`tool_choice: "auto"`
2. **Required：** 调用一个或多个函数。`tool_choice: "required"`
3. **Forced Function：** 精确调用一个特定函数。`tool_choice: {"type": "function", "name": "get_weather"}`
4. **Allowed tools：** 将模型可以进行的工具调用限制为可用工具的子集。

#### 何时使用 allowed_tools

如果你想在模型请求之间只提供工具的子集，但不修改传入的工具列表，以便最大限度地节省提示缓存的费用，你可能需要配置 `allowed_tools` 列表。

```json
"tool_choice": {
    "type": "allowed_tools",
    "mode": "auto",
    "tools": [
        { "type": "function", "name": "get_weather" },
        { "type": "function", "name": "search_docs" }
    ]
}
```

你也可以将 `tool_choice` 设置为 `"none"` 以模仿不传递任何函数的行为。

### 并行函数调用

模型可能选择在单个轮次中调用多个函数。你可以通过将 `parallel_tool_calls` 设置为 `false` 来防止这种情况，这确保恰好调用零个或一个工具。

注意：目前，如果你使用的是微调模型，并且模型在一个轮次中调用多个函数，则这些调用的严格模式将被禁用。

### 严格模式

将 `strict` 设置为 `true` 将确保函数调用可靠地遵守函数模式，而不是尽力而为。我们建议始终启用严格模式。

在底层，严格模式通过利用我们的结构化输出功能工作，因此引入了一些要求：

1. `additionalProperties` 必须为 `parameters` 中的每个对象设置为 `false`。
2. `properties` 中的所有字段必须标记为 `required`。

你可以通过添加 `null` 作为 `type` 选项来表示可选字段（见下面的示例）。

```json
{
    "type": "function",
    "name": "get_weather",
    "description": "获取给定位置的当前天气。",
    "strict": true,
    "parameters": {
        "type": "object",
        "properties": {
            "location": {
                "type": "string",
                "description": "城市和国家，例如：北京，中国"
            },
            "units": {
                "type": ["string", "null"],
                "enum": ["celsius", "fahrenheit"],
                "description": "返回温度的单位。"
            }
        },
        "required": ["location", "units"],
        "additionalProperties": false
    }
}
```

在 playground 中生成的所有模式都启用了严格模式。

虽然我们建议你启用严格模式，但它有一些限制：

1. 不支持 JSON Schema 的某些功能。（请参阅支持的模式。）

特别是对于微调模型：

1. 模式在第一次请求时进行额外处理（然后被缓存）。如果你的模式因请求而异，这可能导致更高的延迟。
2. 模式为性能而缓存，不符合零数据保留条件。

---

## 流式传输

流式传输可用于通过显示调用了哪个函数以及模型填充其参数来展示进度，甚至可以实时显示参数。

流式函数调用与流式常规响应非常相似：你将 `stream` 设置为 `true` 并获取不同的 `event` 对象。

### 流式函数调用（Python）

```python
from openai import OpenAI

client = OpenAI()

tools = [{
    "type": "function",
    "name": "get_weather",
    "description": "获取给定位置的当前温度。",
    "parameters": {
        "type": "object",
        "properties": {
            "location": {
                "type": "string",
                "description": "城市和国家，例如：北京，中国"
            }
        },
        "required": [
            "location"
        ],
        "additionalProperties": False
    }
}]

stream = client.responses.create(
    model="gpt-4.1",
    input=[{"role": "user", "content": "今天巴黎的天气怎么样？"}],
    tools=tools,
    stream=True
)

for event in stream:
    print(event)
```

### 输出事件

```
{"type":"response.output_item.added","response_id":"resp_1234xyz","output_index":0,"item":{"type":"function_call","id":"fc_1234xyz","call_id":"call_1234xyz","name":"get_weather","arguments":""}}
{"type":"response.function_call_arguments.delta","response_id":"resp_1234xyz","item_id":"fc_1234xyz","output_index":0,"delta":"{\""}
{"type":"response.function_call_arguments.delta","response_id":"resp_1234xyz","item_id":"fc_1234xyz","output_index":0,"delta":"location"}
{"type":"response.function_call_arguments.delta","response_id":"resp_1234xyz","item_id":"fc_1234xyz","output_index":0,"delta":"\":\""}
{"type":"response.function_call_arguments.delta","response_id":"resp_1234xyz","item_id":"fc_1234xyz","output_index":0,"delta":"Paris"}
{"type":"response.function_call_arguments.delta","response_id":"resp_1234xyz","item_id":"fc_1234xyz","output_index":0,"delta":","}
{"type":"response.function_call_arguments.delta","response_id":"resp_1234xyz","item_id":"fc_1234xyz","output_index":0,"delta":" France"}
{"type":"response.function_call_arguments.delta","response_id":"resp_1234xyz","item_id":"fc_1234xyz","output_index":0,"delta":"\"}"}
{"type":"response.function_call_arguments.done","response_id":"resp_1234xyz","item_id":"fc_1234xyz","output_index":0,"arguments":"{\"location\":\"Paris, France\"}"}
{"type":"response.output_item.done","response_id":"resp_1234xyz","output_index":0,"item":{"type":"function_call","id":"fc_1234xyz","call_id":"call_1234xyz","name":"get_weather","arguments":"{\"location\":\"Paris, France\"}"}}
```

与将块聚合为单个 `content` 字符串不同，你是将块聚合为编码的 `arguments` JSON 对象。

当模型调用一个或多个函数时，将为每个函数调用发出 `response.output_item.added` 类型的事件，包含以下字段：

| 字段 | 描述 |
|------|------|
| response_id | 函数调用所属响应的 ID |
| output_index | 响应中输出项的索引。这代表响应中的各个函数调用。 |
| item | 进行中的函数调用项，包括 name、arguments 和 id 字段 |

之后，你将收到一系列 `response.function_call_arguments.delta` 类型的事件，其中包含 `arguments` 字段的 `delta`。

### 累积 tool_call delta（Python）

```python
final_tool_calls = {}

for event in stream:
    if event.type == 'response.output_item.added':
        final_tool_calls[event.output_index] = event.item
    elif event.type == 'response.function_call_arguments.delta':
        index = event.output_index

        if final_tool_calls[index]:
            final_tool_calls[index].arguments += event.delta
```

### 累积的 final_tool_calls[0]

```json
{
    "type": "function_call",
    "id": "fc_1234xyz",
    "call_id": "call_2345abc",
    "name": "get_weather",
    "arguments": "{\"location\":\"Paris, France\"}"
}
```

当模型完成调用函数时，将发出 `response.function_call_arguments.done` 类型的事件。此事件包含整个函数调用，包括以下字段：

| 字段 | 描述 |
|------|------|
| response_id | 函数调用所属响应的 ID |
| output_index | 响应中输出项的索引。这代表响应中的各个函数调用。 |
| item | 函数调用项，包括 name、arguments 和 id 字段。 |

---

## 自定义工具

自定义工具的工作方式与 JSON Schema 驱动的函数工具大致相同。但与其向模型提供工具需要什么输入的明确说明，模型可以将任意字符串作为输入传回你的工具。这对于避免不必要地将响应包装在 JSON 中，或对响应应用自定义语法（下面详细介绍）很有用。

### 自定义工具调用示例（Python）

```python
from openai import OpenAI

client = OpenAI()

response = client.responses.create(
    model="gpt-5",
    input="使用 code_exec 工具在控制台打印 hello world。",
    tools=[
        {
            "type": "custom",
            "name": "code_exec",
            "description": "执行任意 Python 代码。",
        }
    ]
)
print(response.output)
```

和以前一样，`output` 数组将包含模型生成的工具调用。只是这次，工具调用输入是以纯文本形式给出的。

```json
[
    {
        "id": "rs_6890e972fa7c819ca8bc561526b989170694874912ae0ea6",
        "type": "reasoning",
        "content": [],
        "summary": []
    },
    {
        "id": "ctc_6890e975e86c819c9338825b3e1994810694874912ae0ea6",
        "type": "custom_tool_call",
        "status": "completed",
        "call_id": "call_aGiFQkRWSWAIsMQ19fKqxUgb",
        "input": "print(\"hello world\")",
        "name": "code_exec"
    }
]
```

---

## 上下文无关文法

上下文无关文法（CFG）是一组规则，定义如何以给定格式生成有效文本。对于自定义工具，你可以提供一个 CFG 来约束模型对自定义工具的文本输入。

你可以在配置自定义工具时使用 `grammar` 参数提供自定义 CFG。目前，我们在定义语法时支持两种 CFG 语法：`lark` 和 `regex`。

---

## 总结

函数调用为以下方面提供了强大的机制：

1. **扩展模型能力** - 让模型访问外部数据和功能
2. **结构化交互** - 在你的应用程序和模型之间定义清晰的接口
3. **可靠的输出** - 使用严格模式确保模式合规
4. **实时反馈** - 使用流式传输在调用函数时显示进度

最佳实践：
- 保持函数定义清晰且文档完善
- 使用严格模式以确保可靠的模式遵守
- 处理响应中的多个函数调用
- 在生产之前在 playground 中进行彻底测试
