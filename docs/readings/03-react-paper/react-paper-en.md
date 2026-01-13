# ReAct: Synergizing Reasoning and Acting in Language Models

> Paper: https://arxiv.org/abs/2210.03629
> Authors: Shunyu Yao, Jeffrey Zhao, Dian Yu, Nan Du, Izhak Shafran, Karthik Narasimhan, Yuan Cao
> Published: October 2022 (ICLR 2023)
> Project Site: https://react-lm.github.io/

---

## Abstract

While large language models (LLMs) have demonstrated impressive capabilities across tasks in language understanding and interactive decision making, their abilities for reasoning (e.g. chain-of-thought prompting) and acting (e.g. action plan generation) have primarily been studied as separate topics. In this paper, we explore the use of LLMs to generate both **reasoning traces** and **task-specific actions** in an interleaved manner, allowing for greater synergy between the two:

- **Reasoning traces** help the model induce, track, and update action plans as well as handle exceptions
- **Actions** allow it to interface with external sources, such as knowledge bases or environments, to gather additional information

We apply our approach, named **ReAct**, to a diverse set of language and decision making tasks and demonstrate its effectiveness over state-of-the-art baselines, as well as improved human interpretability and trustworthiness over methods without reasoning or acting components.

---

## 1. Introduction

### The Problem with Separate Reasoning and Acting

Humans possess the remarkable ability to seamlessly combine task-oriented actions with verbal reasoning (or inner speech). Consider the example of cooking in a kitchen:

- Between actions, we may reason in language ("Now that everything is cut, I should heat up the pot for water")
- We may also act to support reasoning ("I need to read the recipe to figure out the ingredient amounts")

This tight integration of acting and reasoning allows humans to:
- Learn new tasks quickly
- Perform robust decision making or reasoning
- Even adjust plans on the fly (e.g., "I don't have salt, so let me use soy sauce instead")

### Previous Approaches

| Approach | Limitation |
|----------|------------|
| **Chain-of-Thought (CoT)** | Uses LLM's internal knowledge, may hallucinate facts |
| **Action Generation** | Generates actions without high-level reasoning |

### ReAct Solution

ReAct prompts LLMs to generate both:
1. **Verbal reasoning traces** - for the model to reason about task solving
2. **Actions** - to interact with external environments (e.g., Wikipedia API)

This allows the model to:
- Induce, track, and update action plans
- Handle exceptions
- Interface with external sources for additional information

---

## 2. The ReAct Paradigm

### Core Components

```
┌─────────────────────────────────────────────────────────────┐
│                     ReAct Agent Loop                         │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│   Observation ──► Thought ──► Action ──► Observation ──►... │
│       ↑                          │                           │
│       └──────────────────────────┘                           │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

### The Three Elements

| Element | Description | Example |
|---------|-------------|---------|
| **Thought** | Reasoning about the current situation | "I need to search for information about X" |
| **Action** | Interaction with external environment | `Search[X]`, `Lookup[term]`, `Finish[answer]` |
| **Observation** | Result from the environment | Search results, lookup results |

### ReAct Trace Example (HotpotQA)

```
Question: What is the elevation range for the area that the eastern sector 
of the Colorado orogeny extends into?

Thought 1: I need to search Colorado orogeny, find the area that the eastern 
sector of the Colorado orogeny extends into, then find the elevation range 
of that area.

Action 1: Search[Colorado orogeny]

Observation 1: The Colorado orogeny was an episode of mountain building 
(an orogeny) in Colorado and surrounding areas.

Thought 2: It does not mention the eastern sector. So I need to look up 
eastern sector.

Action 2: Lookup[eastern sector]

Observation 2: (Result 1 / 1) The eastern sector extends into the High Plains.

Thought 3: The eastern sector of Colorado orogeny extends into the High Plains. 
So I need to search High Plains and find its elevation range.

Action 3: Search[High Plains]

Observation 3: High Plains refers to one of two distinct land regions.

Thought 4: I need to instead search High Plains (United States).

Action 4: Search[High Plains (United States)]

Observation 4: The High Plains are a subregion of the Great Plains. 
From east to west, the High Plains rise in elevation from around 
1,800 to 7,000 ft (550 to 2,130 m).

Thought 5: High Plains rise in elevation from around 1,800 to 7,000 ft, 
so the answer is 1,800 to 7,000 ft.

Action 5: Finish[1,800 to 7,000 ft]
```

---

## 3. Action Space

### For Knowledge-Intensive Tasks (HotpotQA, Fever)

| Action | Description |
|--------|-------------|
| `Search[entity]` | Returns the first 5 sentences from the Wikipedia page if it exists, or suggests similar entities |
| `Lookup[string]` | Returns the next sentence containing the string in the current page |
| `Finish[answer]` | Returns the answer and finishes the task |

### For Decision-Making Tasks (ALFWorld, WebShop)

Domain-specific actions based on the environment:
- ALFWorld: `go to`, `pick up`, `put`, `open`, `close`, `use`, etc.
- WebShop: `search`, `click`, `buy`, etc.

---

## 4. ReAct vs Other Methods

### Comparison Table

| Method | Reasoning | Acting | External Knowledge | Interpretable |
|--------|-----------|--------|-------------------|---------------|
| Standard Prompting | ❌ | ❌ | ❌ | ❌ |
| Chain-of-Thought (CoT) | ✅ | ❌ | ❌ | ✅ |
| Act-only | ❌ | ✅ | ✅ | ❌ |
| **ReAct** | ✅ | ✅ | ✅ | ✅ |

### Key Advantages of ReAct

1. **Reduces Hallucination**: By grounding reasoning in external knowledge
2. **Interpretable**: Shows the model's reasoning process
3. **Flexible**: Can recover from errors by adjusting plans
4. **Generalizable**: Works across different task types

---

## 5. Experimental Results

### HotpotQA (Question Answering)

| Method | Exact Match |
|--------|-------------|
| Standard | 28.7% |
| CoT | 29.4% |
| Act-only | 25.7% |
| **ReAct** | **34.3%** |
| ReAct + CoT-SC | **35.1%** |

### Fever (Fact Verification)

| Method | Accuracy |
|--------|----------|
| Standard | 57.1% |
| CoT | 56.3% |
| Act-only | 58.9% |
| **ReAct** | **60.9%** |

### ALFWorld (Interactive Decision Making)

| Method | Success Rate |
|--------|--------------|
| BUTLER (Imitation Learning) | 37% |
| Act-only | 45% |
| **ReAct** | **71%** |

### WebShop (Web Navigation)

| Method | Success Rate |
|--------|--------------|
| IL + RL | 29% |
| Act-only | 31% |
| **ReAct** | **40%** |

---

## 6. Error Analysis

### Types of Errors

| Error Type | CoT | ReAct |
|------------|-----|-------|
| Hallucination | High | Low |
| Reasoning Error | Medium | Medium |
| Search Failure | N/A | Present |
| Label Ambiguity | Present | Present |

### ReAct's Error Recovery

```
Thought: The search didn't return useful results. Let me try a different query.
Action: Search[alternative query]
```

ReAct can:
- Detect when information is insufficient
- Reformulate queries
- Try alternative approaches

---

## 7. Synergy: ReAct + CoT

### Best of Both Worlds

```
ReAct + CoT Self-Consistency:
1. Run ReAct to get external knowledge
2. Run CoT for internal reasoning
3. Use majority voting for final answer
```

This combination achieves the best results on knowledge-intensive tasks.

---

## 8. Prompt Design

### Few-Shot Prompting Strategy

The paper uses 6 examples for HotpotQA and 3 examples for Fever, manually annotated with:
- Diverse reasoning patterns
- Different action types
- Error recovery examples

### Prompt Template Structure

```
[Task Description]

[Example 1]
Question: ...
Thought 1: ...
Action 1: ...
Observation 1: ...
...

[Example 2]
...

[Actual Question]
Question: {user_question}
Thought 1:
```

---

## 9. Key Insights

### Why ReAct Works

1. **Grounded Reasoning**: Actions provide real-world feedback
2. **Explicit Planning**: Thoughts make the plan visible and adjustable
3. **Error Handling**: Can detect and recover from mistakes
4. **Interpretability**: Human can understand and trust the process

### Limitations

1. **Prompt Engineering**: Requires careful design of examples
2. **API Dependence**: Relies on external API quality
3. **Computation Cost**: More tokens due to reasoning traces
4. **Error Propagation**: Bad searches can derail reasoning

---

## 10. Implementation Tips

### For Developers

```python
# Simplified ReAct loop
def react_agent(question, tools, max_steps=10):
    context = f"Question: {question}\n"
    
    for step in range(max_steps):
        # Generate thought and action
        response = llm.generate(context + f"Thought {step+1}:")
        thought, action = parse_response(response)
        
        context += f"Thought {step+1}: {thought}\n"
        context += f"Action {step+1}: {action}\n"
        
        # Execute action
        if action.startswith("Finish"):
            return extract_answer(action)
        
        observation = execute_action(action, tools)
        context += f"Observation {step+1}: {observation}\n"
    
    return "Max steps reached"
```

### Best Practices

1. **Clear Action Definitions**: Define available actions explicitly
2. **Good Examples**: Include diverse, representative examples
3. **Error Handling**: Show examples of error recovery
4. **Observation Limits**: Truncate long observations to save tokens

---

## 11. Conclusion

ReAct demonstrates that:
- **Reasoning and acting are synergistic** in LLMs
- **External grounding reduces hallucination**
- **Interpretable traces improve trust**
- **Simple prompting can achieve strong results**

The approach is now widely adopted in agent frameworks like LangChain, AutoGPT, and others.

---

## References

- Original Paper: https://arxiv.org/abs/2210.03629
- Project Website: https://react-lm.github.io/
- Related: Chain-of-Thought Prompting (Wei et al., 2022)
