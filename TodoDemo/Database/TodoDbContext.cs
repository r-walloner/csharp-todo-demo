using Microsoft.EntityFrameworkCore;
using TodoDemo.Entities;

namespace TodoDemo.Database;

public class TodoDbContext(DbContextOptions<TodoDbContext> options) : DbContext(options)
{
    public DbSet<TodoList> TodoLists => Set<TodoList>();
    public DbSet<TodoItem> TodoItems => Set<TodoItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TodoList>(todoList =>
        {
            todoList.HasKey(l => l.Id);
            todoList.HasMany(l => l.Items)
                .WithOne(i => i.TodoList!)
                .HasForeignKey(i => i.TodoListId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TodoItem>(todoItem =>
        {
            todoItem.HasKey(i => i.Id);
            todoItem.HasIndex(i => new { i.TodoListId, i.IsCompleted });
            todoItem.Property(i => i.Priority)
                .HasConversion<string>()
                .HasMaxLength(10);
        });
    }
}