using System.Linq.Expressions;

namespace GWTVisitor;

public class SourcePlayground
{
    protected void Given(string name, Action establish)
    {
        establish();
    }

    private static void ExampleAction()
    {
    }

    private static void ExampleAction(Expression<Func<int, int>> expression) {}

    public void should_success()
    {
        Given("the given", () =>
        {
            ExampleAction(x => x * 2);
            //comment in action
            Console.WriteLine("do sth 1");
            new List<int> {1};
            new List<int>().ForEach(x =>
            {
                //do sth
            });
        });

        Given("xx", SourcePlayground.ExampleAction);
        Given("the given", () => new[] {1}.ToList());

        var act = () => { };
        Given("the given", act);
    }
}