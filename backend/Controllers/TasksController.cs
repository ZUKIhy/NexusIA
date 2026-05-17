using NexusBackend.Services;
using Microsoft.AspNetCore.Mvc;

namespace NexusBackend.Controllers;

[ApiController]
[Route("api/tasks")]
public class TasksController : ControllerBase
{
    private readonly TaskService _tasks;

    public TasksController(TaskService tasks)
    {
        _tasks = tasks;
    }

    [HttpGet]
    public IActionResult GetAll() => Ok(new { content = _tasks.ReadAll() });

    [HttpPost("create")]
    public IActionResult Create([FromBody] CreateTaskRequest request)
    {
        _tasks.Create(request.Content);
        return Ok(new { created = true });
    }
}

public class CreateTaskRequest
{
    public string Content { get; set; } = "";
}
