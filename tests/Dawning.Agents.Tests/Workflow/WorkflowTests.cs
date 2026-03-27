using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.Tools;
using Dawning.Agents.Abstractions.Workflow;
using Dawning.Agents.Core;
using Dawning.Agents.Core.Tools;
using Dawning.Agents.Core.Workflow;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Dawning.Agents.Tests.Workflow;

public class WorkflowTests
{
    #region WorkflowBuilder Tests

    [Fact]
    public void WorkflowBuilder_Create_ReturnsBuilder()
    {
        var builder = WorkflowBuilder.Create("test-workflow", "Test Workflow");

        builder.Should().NotBeNull();
    }

    [Fact]
    public void WorkflowBuilder_Build_WithStartNode_ReturnsDefinition()
    {
        var definition = WorkflowBuilder
            .Create("test-workflow", "Test Workflow")
            .WithDescription("This is a test workflow")
            .WithVersion("1.0.0")
            .AddStartNode("start")
            .AddEndNode("end")
            .Connect("start", "end")
            .Build();

        definition.Should().NotBeNull();
        definition.Id.Should().Be("test-workflow");
        definition.Name.Should().Be("Test Workflow");
        definition.Description.Should().Be("This is a test workflow");
        definition.StartNodeId.Should().Be("start");
        definition.Nodes.Should().HaveCount(2);
        definition.Edges.Should().HaveCount(1);
    }

    [Fact]
    public void WorkflowBuilder_Build_WithoutStartNode_ThrowsException()
    {
        var builder = WorkflowBuilder.Create("test-workflow", "Test Workflow");

        var action = () => builder.Build();

        action.Should().Throw<InvalidOperationException>().WithMessage("*start node*");
    }

    [Fact]
    public void WorkflowBuilder_AddAgentNode_AddsCorrectNode()
    {
        var definition = WorkflowBuilder
            .Create("test", "Test")
            .AddStartNode("start")
            .AddAgentNode("agent1", "Agent Node", "TestAgent", "{{input}}", maxRetries: 3)
            .AddEndNode("end")
            .Connect("start", "agent1")
            .Connect("agent1", "end")
            .Build();

        var agentNode = definition.Nodes.First(n => n.Id == "agent1");
        agentNode.Type.Should().Be(WorkflowNodeType.Agent);
        agentNode.Config.Should().ContainKey("agentName");
        agentNode.Config!["agentName"].Should().Be("TestAgent");
    }

    [Fact]
    public void WorkflowBuilder_AddToolNode_AddsCorrectNode()
    {
        var definition = WorkflowBuilder
            .Create("test", "Test")
            .AddStartNode("start")
            .AddToolNode("tool1", "Tool Node", "Calculator", "2+2")
            .AddEndNode("end")
            .Connect("start", "tool1")
            .Connect("tool1", "end")
            .Build();

        var toolNode = definition.Nodes.First(n => n.Id == "tool1");
        toolNode.Type.Should().Be(WorkflowNodeType.Tool);
        toolNode.Config.Should().ContainKey("toolName");
        toolNode.Config!["toolName"].Should().Be("Calculator");
    }

    [Fact]
    public void WorkflowBuilder_AddConditionNode_AddsCorrectNode()
    {
        var definition = WorkflowBuilder
            .Create("test", "Test")
            .AddStartNode("start")
            .AddConditionNode(
                "condition1",
                "Condition",
                c =>
                    c.InputFrom("lastOutput")
                        .AddBranch("Yes", "contains:yes", "yes_branch")
                        .AddBranch("No", "contains:no", "no_branch")
                        .DefaultTo("default_branch")
            )
            .AddEndNode("end")
            .StartWith("start")
            .Build();

        var conditionNode = definition.Nodes.First(n => n.Id == "condition1");
        conditionNode.Type.Should().Be(WorkflowNodeType.Condition);
        conditionNode.Config.Should().ContainKey("branches");
    }

    [Fact]
    public void WorkflowBuilder_AddParallelNode_AddsCorrectNode()
    {
        var definition = WorkflowBuilder
            .Create("test", "Test")
            .AddStartNode("start")
            .AddParallelNode(
                "parallel1",
                "Parallel Execution",
                p =>
                    p.AddBranch("Branch1", "branch1_start")
                        .AddBranch("Branch2", "branch2_start")
                        .WaitAll()
                        .ConcatenateResults()
            )
            .AddEndNode("end")
            .StartWith("start")
            .Build();

        var parallelNode = definition.Nodes.First(n => n.Id == "parallel1");
        parallelNode.Type.Should().Be(WorkflowNodeType.Parallel);
        parallelNode.Config.Should().ContainKey("branches");
    }

    [Fact]
    public void WorkflowBuilder_AddLoopNode_AddsCorrectNode()
    {
        var definition = WorkflowBuilder
            .Create("test", "Test")
            .AddStartNode("start")
            .AddLoopNode("loop1", "Loop Node", "loop_body", maxIterations: 5)
            .AddEndNode("end")
            .StartWith("start")
            .Build();

        var loopNode = definition.Nodes.First(n => n.Id == "loop1");
        loopNode.Type.Should().Be(WorkflowNodeType.Loop);
        loopNode.Config!["maxIterations"].Should().Be(5);
    }

    [Fact]
    public void WorkflowBuilder_AddDelayNode_AddsCorrectNode()
    {
        var definition = WorkflowBuilder
            .Create("test", "Test")
            .AddStartNode("start")
            .AddDelayNode("delay1", "Delay", 5000)
            .AddEndNode("end")
            .Connect("start", "delay1")
            .Connect("delay1", "end")
            .Build();

        var delayNode = definition.Nodes.First(n => n.Id == "delay1");
        delayNode.Type.Should().Be(WorkflowNodeType.Delay);
        delayNode.Config!["delayMs"].Should().Be(5000);
    }

    [Fact]
    public void WorkflowBuilder_AddHumanApprovalNode_AddsCorrectNode()
    {
        var definition = WorkflowBuilder
            .Create("test", "Test")
            .AddStartNode("start")
            .AddHumanApprovalNode(
                "approval1",
                "Human Approval",
                "approved_node",
                "rejected_node",
                "Please confirm the operation"
            )
            .AddEndNode("end")
            .StartWith("start")
            .Build();

        var approvalNode = definition.Nodes.First(n => n.Id == "approval1");
        approvalNode.Type.Should().Be(WorkflowNodeType.HumanApproval);
        approvalNode.Config!["approvedNodeId"].Should().Be("approved_node");
    }

    [Fact]
    public void WorkflowBuilder_WithMetadata_AddsMetadata()
    {
        var definition = WorkflowBuilder
            .Create("test", "Test")
            .WithMetadata("author", "Test")
            .WithMetadata("category", "Testing")
            .AddStartNode("start")
            .AddEndNode("end")
            .Connect("start", "end")
            .Build();

        definition.Metadata.Should().ContainKey("author");
        definition.Metadata!["author"].Should().Be("Test");
    }

    #endregion

    #region WorkflowEngine Tests

    [Fact]
    public void WorkflowEngine_Validate_ValidWorkflow_ReturnsIsValid()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var sp = services.BuildServiceProvider();

        var engine = new WorkflowEngine(sp);
        var definition = WorkflowBuilder
            .Create("test", "Test")
            .AddStartNode("start")
            .AddEndNode("end")
            .Connect("start", "end")
            .Build();

        var result = engine.Validate(definition);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void WorkflowEngine_Validate_InvalidStartNode_ReturnsError()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var sp = services.BuildServiceProvider();

        var engine = new WorkflowEngine(sp);
        var definition = new WorkflowDefinition
        {
            Id = "test",
            Name = "Test",
            StartNodeId = "nonexistent",
            Nodes = [new WorkflowNodeDefinition { Id = "start", Name = "Start" }],
            Edges = [],
        };

        var result = engine.Validate(definition);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "INVALID_START_NODE");
    }

    [Fact]
    public void WorkflowEngine_Validate_InvalidEdge_ReturnsError()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var sp = services.BuildServiceProvider();

        var engine = new WorkflowEngine(sp);
        var definition = new WorkflowDefinition
        {
            Id = "test",
            Name = "Test",
            StartNodeId = "start",
            Nodes = [new WorkflowNodeDefinition { Id = "start", Name = "Start" }],
            Edges = [new WorkflowEdgeDefinition { FromNodeId = "start", ToNodeId = "nonexistent" }],
        };

        var result = engine.Validate(definition);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "INVALID_EDGE_TARGET");
    }

    [Fact]
    public void WorkflowEngine_Validate_NoEndNode_ReturnsWarning()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var sp = services.BuildServiceProvider();

        var engine = new WorkflowEngine(sp);
        var definition = WorkflowBuilder
            .Create("test", "Test")
            .AddStartNode("start")
            .AddAgentNode("agent1", "Agent", "TestAgent")
            .Connect("start", "agent1")
            .Build();

        var result = engine.Validate(definition);

        result.IsValid.Should().BeTrue();
        result.Warnings.Should().Contain(w => w.Code == "NO_END_NODE");
    }

    [Fact]
    public async Task WorkflowEngine_ExecuteAsync_SimpleWorkflow_Succeeds()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var sp = services.BuildServiceProvider();

        var engine = new WorkflowEngine(sp);
        var definition = WorkflowBuilder
            .Create("test", "Test")
            .AddStartNode("start")
            .AddEndNode("end")
            .Connect("start", "end")
            .Build();

        var context = new WorkflowContext { Input = "Hello" };

        var result = await engine.ExecuteAsync(definition, context);

        result.Success.Should().BeTrue();
        result.WorkflowId.Should().Be("test");
        result.NodesExecuted.Should().Be(2);
    }

    [Fact]
    public async Task WorkflowEngine_ExecuteAsync_WithDelayNode_Delays()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var sp = services.BuildServiceProvider();

        var engine = new WorkflowEngine(sp);
        var definition = WorkflowBuilder
            .Create("test", "Test")
            .AddStartNode("start")
            .AddDelayNode("delay", "Delay", 100)
            .AddEndNode("end")
            .Connect("start", "delay")
            .Connect("delay", "end")
            .Build();

        var context = new WorkflowContext { Input = "Hello" };

        var result = await engine.ExecuteAsync(definition, context);

        result.Success.Should().BeTrue();
        // Allow small timing tolerance (due to system scheduling)
        result.TotalDurationMs.Should().BeGreaterThanOrEqualTo(95);
    }

    [Fact]
    public async Task WorkflowEngine_ExecuteAsync_AgentNode_ShouldTreatSwallowedCancellationAsCancelled()
    {
        using var cts = new CancellationTokenSource();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IAgent>(
            new CancellationSwallowingAgent("TestAgent", () => cts.Cancel())
        );
        var sp = services.BuildServiceProvider();

        var engine = new WorkflowEngine(sp);
        var definition = WorkflowBuilder
            .Create("test", "Test")
            .AddStartNode("start")
            .AddAgentNode("agent", "Agent", "TestAgent")
            .AddEndNode("end")
            .Connect("start", "agent")
            .Connect("agent", "end")
            .Build();

        var context = new WorkflowContext { Input = "Hello" };
        var act = async () => await engine.ExecuteAsync(definition, context, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task WorkflowEngine_ExecuteAsync_ToolNode_ShouldTreatSwallowedCancellationAsCancelled()
    {
        using var cts = new CancellationTokenSource();

        var toolRegistry = new ToolRegistry();
        toolRegistry.Register(new CancellationSwallowingTool("CancelableTool", () => cts.Cancel()));

        var services = new ServiceCollection();
        services.AddLogging();
        var sp = services.BuildServiceProvider();

        var engine = new WorkflowEngine(sp, toolRegistry);
        var definition = WorkflowBuilder
            .Create("test", "Test")
            .AddStartNode("start")
            .AddToolNode("tool", "Tool", "CancelableTool")
            .AddEndNode("end")
            .Connect("start", "tool")
            .Connect("tool", "end")
            .Build();

        var context = new WorkflowContext { Input = "Hello" };
        var act = async () => await engine.ExecuteAsync(definition, context, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void WorkflowEngine_CreateWorkflow_ValidDefinition_ReturnsWorkflow()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var sp = services.BuildServiceProvider();

        var engine = new WorkflowEngine(sp);
        var definition = WorkflowBuilder
            .Create("test", "Test")
            .AddStartNode("start")
            .AddEndNode("end")
            .Connect("start", "end")
            .Build();

        var workflow = engine.CreateWorkflow(definition);

        workflow.Should().NotBeNull();
        workflow.Id.Should().Be("test");
        workflow.Name.Should().Be("Test");
    }

    [Fact]
    public void WorkflowEngine_CreateWorkflow_InvalidDefinition_ThrowsException()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var sp = services.BuildServiceProvider();

        var engine = new WorkflowEngine(sp);
        var definition = new WorkflowDefinition
        {
            Id = "test",
            Name = "Test",
            StartNodeId = "nonexistent",
            Nodes = [],
            Edges = [],
        };

        var action = () => engine.CreateWorkflow(definition);

        action.Should().Throw<InvalidOperationException>().WithMessage("*Invalid*");
    }

    [Fact]
    public void WorkflowEngine_Constructor_NullServiceProvider_ThrowsArgumentNullException()
    {
        var act = () => new WorkflowEngine(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("serviceProvider");
    }

    #endregion

    #region WorkflowContext Tests

    [Fact]
    public void WorkflowContext_SetState_GetState_ReturnsValue()
    {
        var context = new WorkflowContext();

        context.SetState("key1", "value1");
        context.SetState("key2", 123);

        context.GetState<string>("key1").Should().Be("value1");
        context.GetState<int>("key2").Should().Be(123);
    }

    [Fact]
    public void WorkflowContext_GetState_NotFound_ReturnsDefault()
    {
        var context = new WorkflowContext();

        context.GetState<string>("nonexistent").Should().BeNull();
        context.GetState<int>("nonexistent").Should().Be(0);
    }

    [Fact]
    public void WorkflowContext_GetLastResult_EmptyHistory_ReturnsNull()
    {
        var context = new WorkflowContext();

        context.GetLastResult().Should().BeNull();
    }

    [Fact]
    public void WorkflowContext_GetLastResult_WithHistory_ReturnsLastResult()
    {
        var context = new WorkflowContext();
        var result = NodeExecutionResult.Ok("node1", "output");
        context.AddNodeResult("node1", result);
        context.AddExecutionStep(
            new WorkflowExecutionStep
            {
                NodeId = "node1",
                NodeName = "Node 1",
                StartedAt = DateTimeOffset.UtcNow,
            }
        );

        var lastResult = context.GetLastResult();

        lastResult.Should().NotBeNull();
        lastResult!.NodeId.Should().Be("node1");
        lastResult.Output.Should().Be("output");
    }

    #endregion

    #region NodeExecutionResult Tests

    [Fact]
    public void NodeExecutionResult_Ok_CreatesSuccessResult()
    {
        var result = NodeExecutionResult.Ok("node1", "output");

        result.Success.Should().BeTrue();
        result.NodeId.Should().Be("node1");
        result.Output.Should().Be("output");
        result.Error.Should().BeNull();
    }

    [Fact]
    public void NodeExecutionResult_Fail_CreatesFailureResult()
    {
        var result = NodeExecutionResult.Fail("node1", "error message");

        result.Success.Should().BeFalse();
        result.NodeId.Should().Be("node1");
        result.Error.Should().Be("error message");
    }

    [Fact]
    public void NodeExecutionResult_Branch_CreatesResultWithNextNode()
    {
        var result = NodeExecutionResult.Branch("node1", "node2");

        result.Success.Should().BeTrue();
        result.NodeId.Should().Be("node1");
        result.NextNodeId.Should().Be("node2");
    }

    #endregion

    #region WorkflowSerializer Tests

    [Fact]
    public void WorkflowSerializer_SerializeToJson_ReturnsValidJson()
    {
        var serializer = new WorkflowSerializer();
        var definition = WorkflowBuilder
            .Create("test", "Test Workflow")
            .WithDescription("Test Description")
            .AddStartNode("start")
            .AddEndNode("end")
            .Connect("start", "end")
            .Build();

        var json = serializer.SerializeToJson(definition);

        json.Should().Contain("\"id\": \"test\"");
        // JSON serialization uses Unicode encoding
        json.Should().Contain("\"name\":");
    }

    [Fact]
    public void WorkflowSerializer_DeserializeFromJson_ReturnsDefinition()
    {
        var serializer = new WorkflowSerializer();
        var json = """
            {
                "id": "test",
                "name": "Test Workflow",
                "startNodeId": "start",
                "nodes": [
                    { "id": "start", "name": "Start", "type": "start" },
                    { "id": "end", "name": "End", "type": "end" }
                ],
                "edges": [
                    { "fromNodeId": "start", "toNodeId": "end" }
                ]
            }
            """;

        var definition = serializer.DeserializeFromJson(json);

        definition.Id.Should().Be("test");
        definition.Name.Should().Be("Test Workflow");
        definition.Nodes.Should().HaveCount(2);
    }

    [Fact]
    public void WorkflowSerializer_RoundTrip_PreservesData()
    {
        var serializer = new WorkflowSerializer();
        var original = WorkflowBuilder
            .Create("test", "Test Workflow")
            .WithDescription("Test Description")
            .WithVersion("2.0.0")
            .AddStartNode("start")
            .AddAgentNode("agent1", "Agent Node", "TestAgent")
            .AddEndNode("end")
            .Connect("start", "agent1")
            .Connect("agent1", "end")
            .Build();

        var json = serializer.SerializeToJson(original);
        var restored = serializer.DeserializeFromJson(json);

        restored.Id.Should().Be(original.Id);
        restored.Name.Should().Be(original.Name);
        restored.Description.Should().Be(original.Description);
        restored.Version.Should().Be(original.Version);
        restored.Nodes.Should().HaveCount(original.Nodes.Count);
        restored.Edges.Should().HaveCount(original.Edges.Count);
    }

    [Fact]
    public void WorkflowSerializer_SerializeToYaml_ReturnsValidYaml()
    {
        var serializer = new WorkflowSerializer();
        var definition = WorkflowBuilder
            .Create("test", "Test Workflow")
            .AddStartNode("start")
            .AddEndNode("end")
            .Connect("start", "end")
            .Build();

        var yaml = serializer.SerializeToYaml(definition);

        yaml.Should().Contain("id: test");
        yaml.Should().Contain("name: Test Workflow");
        yaml.Should().Contain("nodes:");
        yaml.Should().Contain("edges:");
    }

    [Fact]
    public void WorkflowSerializer_DeserializeFromYaml_ReturnsDefinition()
    {
        var serializer = new WorkflowSerializer();
        var yaml = """
            id: test
            name: Test Workflow
            startNodeId: start

            nodes:
              - id: start
                name: Start
                type: Start
              - id: end
                name: End
                type: End

            edges:
              - from: start
                to: end
            """;

        var definition = serializer.DeserializeFromYaml(yaml);

        definition.Id.Should().Be("test");
        definition.Name.Should().Be("Test Workflow");
        definition.Nodes.Should().HaveCount(2);
        definition.Edges.Should().HaveCount(1);
    }

    #endregion

    #region WorkflowVisualizer Tests

    [Fact]
    public void WorkflowVisualizer_GenerateMermaid_ReturnsValidMermaid()
    {
        var visualizer = new WorkflowVisualizer();
        var definition = WorkflowBuilder
            .Create("test", "Test Workflow")
            .AddStartNode("start")
            .AddAgentNode("agent1", "Process Request", "TestAgent")
            .AddConditionNode(
                "check",
                "Check Result",
                c => c.AddBranch("Yes", "contains:yes", "end").DefaultTo("agent1")
            )
            .AddEndNode("end")
            .Connect("start", "agent1")
            .Connect("agent1", "check")
            .Connect("check", "end", "Success")
            .Build();

        var mermaid = visualizer.GenerateMermaid(definition);

        mermaid.Should().Contain("flowchart TD");
        mermaid.Should().Contain("start");
        mermaid.Should().Contain("agent1");
        mermaid.Should().Contain("-->");
        mermaid.Should().Contain("style");
    }

    [Fact]
    public void WorkflowVisualizer_GenerateDot_ReturnsValidDot()
    {
        var visualizer = new WorkflowVisualizer();
        var definition = WorkflowBuilder
            .Create("test", "Test Workflow")
            .AddStartNode("start")
            .AddEndNode("end")
            .Connect("start", "end")
            .Build();

        var dot = visualizer.GenerateDot(definition);

        dot.Should().Contain("digraph workflow");
        dot.Should().Contain("start");
        dot.Should().Contain("end");
        dot.Should().Contain("->");
    }

    [Fact]
    public void WorkflowVisualizer_GenerateMermaid_DifferentNodeTypes_DifferentShapes()
    {
        var visualizer = new WorkflowVisualizer();
        var definition = WorkflowBuilder
            .Create("test", "Test")
            .AddStartNode("start")
            .AddConditionNode("cond", "Condition", c => c.DefaultTo("end"))
            .AddParallelNode("parallel", "Parallel", p => p.AddBranch("B1", "end"))
            .AddLoopNode("loop", "Loop", "end")
            .AddEndNode("end")
            .StartWith("start")
            .Build();

        var mermaid = visualizer.GenerateMermaid(definition);

        // Verify different node types have different shapes
        mermaid.Should().Contain("(("); // Circle (start/end)
        mermaid.Should().Contain("{"); // Diamond (condition)
        mermaid.Should().Contain("[["); // Double border (parallel)
        mermaid.Should().Contain("(["); // Stadium/pill shape (loop)
    }

    #endregion

    #region DI Extension Tests

    [Fact]
    public void AddWorkflowEngine_RegistersAllServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddWorkflowEngine();
        var sp = services.BuildServiceProvider();

        var engine = sp.GetService<IWorkflowEngine>();
        var serializer = sp.GetService<IWorkflowSerializer>();
        var visualizer = sp.GetService<IWorkflowVisualizer>();

        engine.Should().NotBeNull();
        serializer.Should().NotBeNull();
        visualizer.Should().NotBeNull();
    }

    [Fact]
    public void AddWorkflow_RegistersWorkflowDefinition()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddWorkflowEngine();
        services.AddWorkflow(
            "test-workflow",
            "Test Workflow",
            builder =>
            {
                builder.AddStartNode("start").AddEndNode("end").Connect("start", "end");
            }
        );
        var sp = services.BuildServiceProvider();

        var definitions = sp.GetServices<WorkflowDefinition>().ToList();

        definitions.Should().HaveCount(1);
        definitions[0].Id.Should().Be("test-workflow");
    }

    #endregion
}

file sealed class CancellationSwallowingAgent(string name, Action cancel) : IAgent
{
    public string Name => name;

    public string Instructions => "test";

    public Task<AgentResponse> RunAsync(string input, CancellationToken cancellationToken = default)
    {
        cancel();
        return Task.FromResult(AgentResponse.Failed("agent-failed", [], TimeSpan.Zero));
    }

    public Task<AgentResponse> RunAsync(
        AgentContext context,
        CancellationToken cancellationToken = default
    )
    {
        return RunAsync(context.UserInput, cancellationToken);
    }
}

file sealed class CancellationSwallowingTool(string name, Action cancel) : ITool
{
    public string Name => name;

    public string Description => "test";

    public string ParametersSchema => "{\"type\":\"object\"}";

    public bool RequiresConfirmation => false;

    public ToolRiskLevel RiskLevel => ToolRiskLevel.Low;

    public string? Category => "test";

    public Task<ToolResult> ExecuteAsync(
        string input,
        CancellationToken cancellationToken = default
    )
    {
        cancel();
        return Task.FromResult(ToolResult.Fail("tool-failed"));
    }
}
