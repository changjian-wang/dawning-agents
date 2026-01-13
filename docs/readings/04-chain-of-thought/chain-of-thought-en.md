# Chain-of-Thought Prompting: Eliciting Reasoning in Large Language Models

> Paper: https://arxiv.org/abs/2201.11903
> Authors: Jason Wei, Xuezhi Wang, Dale Schuurmans, Maarten Bosma, Brian Ichter, Fei Xia, Ed Chi, Quoc Le, Denny Zhou
> Published: January 2022 (NeurIPS 2022)

---

## Abstract

We explore how generating a **chain of thought**—a series of intermediate reasoning steps—significantly improves the ability of large language models to perform complex reasoning. In particular, we show how such reasoning abilities emerge naturally in sufficiently large language models via a simple method called **chain-of-thought prompting**, where a few chain of thought demonstrations are provided as exemplars in prompting.

Experiments on three large language models show that chain-of-thought prompting improves performance on a range of arithmetic, commonsense, and symbolic reasoning tasks. The empirical gains can be striking—for instance, prompting a **540B-parameter language model with just eight chain-of-thought exemplars achieves state-of-the-art accuracy on the GSM8K benchmark** of math word problems, surpassing even finetuned GPT-3 with a verifier.

---

## 1. Introduction

### The Challenge of Complex Reasoning

While large language models (LLMs) have achieved remarkable success across many NLP tasks, they still struggle with tasks requiring multi-step reasoning, such as:

- **Mathematical reasoning**: Solving word problems
- **Commonsense reasoning**: Understanding everyday scenarios
- **Symbolic reasoning**: Following logical rules

### The Solution: Chain-of-Thought

Instead of directly outputting an answer, the model is prompted to produce a **series of intermediate reasoning steps** that lead to the final answer.

```
Standard Prompting:
Q: Roger has 5 tennis balls. He buys 2 more cans of tennis balls. 
   Each can has 3 tennis balls. How many tennis balls does he have now?
A: 11

Chain-of-Thought Prompting:
Q: Roger has 5 tennis balls. He buys 2 more cans of tennis balls. 
   Each can has 3 tennis balls. How many tennis balls does he have now?
A: Roger started with 5 balls. 2 cans of 3 tennis balls each is 6 tennis balls. 
   5 + 6 = 11. The answer is 11.
```

---

## 2. Chain-of-Thought Prompting

### Definition

**Chain-of-Thought (CoT)** prompting is a technique where the model is encouraged to generate intermediate reasoning steps before arriving at the final answer.

### Key Properties

| Property | Description |
|----------|-------------|
| **Decomposition** | Breaks down complex problems into smaller steps |
| **Interpretability** | Makes the model's reasoning process visible |
| **Emergent Ability** | Only appears in sufficiently large models |
| **Few-Shot** | Requires only a few demonstration examples |

### How It Works

```
┌─────────────────────────────────────────────────────────┐
│                   CoT Prompting Flow                     │
├─────────────────────────────────────────────────────────┤
│                                                          │
│  [Few-Shot Examples with Reasoning]                      │
│           ↓                                              │
│  [New Question]                                          │
│           ↓                                              │
│  [Model Generates Reasoning Steps]                       │
│           ↓                                              │
│  [Final Answer]                                          │
│                                                          │
└─────────────────────────────────────────────────────────┘
```

---

## 3. Few-Shot Chain-of-Thought

### The Basic Approach

Provide the model with a few examples that include step-by-step reasoning:

```
Example prompt:

Q: The cafeteria had 23 apples. If they used 20 to make lunch and bought 6 more, 
   how many apples do they have?
A: The cafeteria had 23 apples originally. They used 20 to make lunch. 
   So they had 23 - 20 = 3. They bought 6 more apples, so they have 3 + 6 = 9. 
   The answer is 9.

Q: I went to the market and bought 10 apples. I gave 2 apples to the neighbor 
   and 2 to the repairman. I then went and bought 5 more apples and ate 1. 
   How many apples did I remain with?
A: [Model generates reasoning and answer]
```

### Example: Arithmetic Reasoning

**Without CoT:**
```
Q: A juggler can juggle 16 balls. Half of the balls are golf balls, 
   and half of the golf balls are blue. How many blue golf balls are there?
A: 8 (incorrect)
```

**With CoT:**
```
Q: A juggler can juggle 16 balls. Half of the balls are golf balls, 
   and half of the golf balls are blue. How many blue golf balls are there?
A: Half of 16 balls is 8 balls that are golf balls. 
   Half of 8 golf balls is 4 blue golf balls. 
   The answer is 4. (correct)
```

---

## 4. Zero-Shot Chain-of-Thought

### The Magic Phrase

Adding the simple phrase **"Let's think step by step"** to the prompt can trigger chain-of-thought reasoning without any examples!

> Reference: "Large Language Models are Zero-Shot Reasoners" (Kojima et al., 2022)

### How It Works

```
Standard Zero-Shot:
Q: A juggler can juggle 16 balls. Half of the balls are golf balls, 
   and half of the golf balls are blue. How many blue golf balls are there?
A: 8 (incorrect)

Zero-Shot CoT:
Q: A juggler can juggle 16 balls. Half of the balls are golf balls, 
   and half of the golf balls are blue. How many blue golf balls are there?
Let's think step by step.
A: First, we know the juggler can juggle 16 balls. 
   Half of 16 is 8, so there are 8 golf balls. 
   Half of the golf balls (8) are blue, which is 4. 
   Therefore, there are 4 blue golf balls. (correct)
```

### Effective Trigger Phrases

| Phrase | Effectiveness |
|--------|---------------|
| "Let's think step by step" | ✅ Most effective |
| "Let's work this out in a step by step way" | ✅ Very effective |
| "First, let me think about this carefully" | ✅ Effective |
| "Think step by step" | ⚠️ Less effective |

---

## 5. Automatic Chain-of-Thought (Auto-CoT)

### The Problem with Manual CoT

Manually creating chain-of-thought demonstrations is:
- Time-consuming
- Requires domain expertise
- May not generalize well

### Auto-CoT Solution

> Reference: "Automatic Chain of Thought Prompting in Large Language Models" (Zhang et al., 2022)

Auto-CoT automatically constructs demonstrations by:

1. **Clustering** questions into groups
2. **Selecting** representative questions from each cluster
3. **Generating** reasoning chains using Zero-Shot-CoT

```
┌─────────────────────────────────────────────────────────┐
│                   Auto-CoT Pipeline                      │
├─────────────────────────────────────────────────────────┤
│                                                          │
│  [Question Pool]                                         │
│       ↓                                                  │
│  [Cluster by Similarity]                                 │
│       ↓                                                  │
│  [Select Representative Questions]                       │
│       ↓                                                  │
│  [Generate Rationales via Zero-Shot-CoT]                │
│       ↓                                                  │
│  [Automatic Demonstrations]                              │
│                                                          │
└─────────────────────────────────────────────────────────┘
```

### Benefits of Auto-CoT

| Benefit | Description |
|---------|-------------|
| **No Manual Effort** | Automatically generates demonstrations |
| **Diversity** | Samples from different question clusters |
| **Scalability** | Works across different domains |
| **Performance** | Matches or exceeds manual CoT in many cases |

---

## 6. Self-Consistency

### Beyond Greedy Decoding

**Self-Consistency** improves CoT by:
1. Sampling **multiple** reasoning paths
2. Taking a **majority vote** on the final answers

> Reference: "Self-Consistency Improves Chain of Thought Reasoning in Language Models" (Wang et al., 2022)

### How It Works

```
Question: If there are 3 cars in the parking lot and 2 more cars arrive, 
          how many cars are in the parking lot?

Path 1: There are 3 cars. 2 more arrive. 3 + 2 = 5. Answer: 5
Path 2: Start with 3, add 2, get 5. Answer: 5
Path 3: 3 cars + 2 cars = 5 cars. Answer: 5

Majority Vote → Answer: 5
```

### Performance Gains

| Task | CoT | CoT + Self-Consistency |
|------|-----|------------------------|
| GSM8K | 57% | 74% |
| SVAMP | 79% | 86% |
| AQuA | 48% | 58% |

---

## 7. Experimental Results

### Arithmetic Reasoning

| Model + Method | GSM8K | SVAMP | ASDiv |
|----------------|-------|-------|-------|
| GPT-3 (Standard) | 15.6% | 66.4% | 71.3% |
| GPT-3 (CoT) | 46.9% | 74.5% | 76.9% |
| PaLM 540B (CoT) | **58.1%** | **79.0%** | **80.4%** |

### Commonsense Reasoning

| Model + Method | CommonsenseQA | StrategyQA |
|----------------|---------------|------------|
| GPT-3 (Standard) | 73.0% | 63.4% |
| GPT-3 (CoT) | 78.0% | 73.4% |

### Symbolic Reasoning

| Task | Standard | Chain-of-Thought |
|------|----------|------------------|
| Last Letter (2 words) | 1.4% | 99.6% |
| Last Letter (4 words) | 0.2% | 98.8% |
| Coin Flip (4 flips) | 53.8% | 100% |

---

## 8. Why Does CoT Work?

### Key Insights

1. **Decomposition**: Breaking complex problems into simpler sub-problems
2. **Explicit Computation**: Allowing the model to "show its work"
3. **Error Localization**: Easier to identify where reasoning went wrong
4. **Attention Guidance**: Directing the model's focus to relevant information

### Emergent Ability

CoT only helps with **sufficiently large models**:

| Model Size | CoT Benefit |
|------------|-------------|
| < 10B parameters | Little to no improvement |
| 10B - 100B parameters | Moderate improvement |
| > 100B parameters | Significant improvement |

---

## 9. Limitations

### When CoT Fails

1. **Simple Tasks**: Overhead without benefit
2. **Knowledge Gaps**: Can't reason about unknown facts
3. **Calculation Errors**: Still makes arithmetic mistakes
4. **Hallucination**: May generate plausible but incorrect reasoning

### Failure Example

```
Q: What is 27 × 43?
A: Let's think step by step.
   27 × 43 = 27 × 40 + 27 × 3
   = 1080 + 81
   = 1161 (correct answer: 1161)

Q: What is 1847 × 9428?
A: Let's think step by step.
   1847 × 9428 ≈ 17,000,000 (incorrect - real answer: 17,417,516)
```

---

## 10. Best Practices

### Prompt Design Tips

| Tip | Description |
|-----|-------------|
| **Clear Steps** | Use numbered or sequential reasoning |
| **Show All Work** | Include intermediate calculations |
| **Consistent Format** | Use the same structure in all examples |
| **Diverse Examples** | Cover different problem types |
| **Verify Logic** | Ensure example reasoning is correct |

### Example Template

```
Q: [Question]
A: Let's solve this step by step:
   Step 1: [First reasoning step]
   Step 2: [Second reasoning step]
   Step 3: [Third reasoning step]
   Therefore, the answer is [Final Answer].
```

### Combining Techniques

```
Best Performance = CoT + Self-Consistency + Larger Model
```

---

## 11. Applications

### Use Cases for CoT

| Domain | Application |
|--------|-------------|
| **Education** | Math tutoring with step-by-step solutions |
| **Customer Support** | Logical troubleshooting |
| **Legal** | Case analysis with reasoning |
| **Medical** | Diagnostic reasoning |
| **Programming** | Algorithm design and debugging |

### Integration with Agents

CoT is often combined with other techniques:
- **ReAct**: CoT + Actions
- **Tree of Thoughts**: CoT + Search
- **Self-Ask**: CoT + Decomposition

---

## 12. Code Example

### Simple CoT Implementation

```python
def chain_of_thought_prompt(question: str, examples: list[dict]) -> str:
    """Create a CoT prompt with examples."""
    prompt = ""
    
    # Add few-shot examples
    for ex in examples:
        prompt += f"Q: {ex['question']}\n"
        prompt += f"A: {ex['reasoning']}\n\n"
    
    # Add the actual question
    prompt += f"Q: {question}\n"
    prompt += "A: Let's think step by step.\n"
    
    return prompt

# Example usage
examples = [
    {
        "question": "Roger has 5 tennis balls. He buys 2 more cans of 3 balls each. How many does he have?",
        "reasoning": "Roger started with 5 balls. He bought 2 cans with 3 balls each, that's 2 × 3 = 6 balls. Total: 5 + 6 = 11 balls. The answer is 11."
    }
]

question = "Sarah has 8 cookies. She gives 3 to her friend and bakes 5 more. How many does she have?"
prompt = chain_of_thought_prompt(question, examples)
response = llm.generate(prompt)
```

### Zero-Shot CoT

```python
def zero_shot_cot(question: str) -> str:
    """Simple zero-shot CoT prompt."""
    prompt = f"""Q: {question}

Let's think step by step."""
    
    return llm.generate(prompt)
```

---

## 13. Summary

### Key Takeaways

| Aspect | Description |
|--------|-------------|
| **Core Idea** | Generate intermediate reasoning steps |
| **Few-Shot CoT** | Provide examples with reasoning |
| **Zero-Shot CoT** | Use "Let's think step by step" |
| **Auto-CoT** | Automatically generate demonstrations |
| **Self-Consistency** | Sample multiple paths, vote on answer |
| **Emergence** | Only works well with large models |

### Impact

Chain-of-Thought prompting has become a foundational technique in modern LLM applications, enabling:
- Better reasoning on complex tasks
- More interpretable AI outputs
- Foundation for advanced techniques like ReAct and Tree of Thoughts

---

## References

- Original Paper: https://arxiv.org/abs/2201.11903
- Zero-Shot CoT: https://arxiv.org/abs/2205.11916
- Auto-CoT: https://arxiv.org/abs/2210.03493
- Self-Consistency: https://arxiv.org/abs/2203.11171
- Prompting Guide: https://www.promptingguide.ai/techniques/cot
