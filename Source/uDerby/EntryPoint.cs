using uScoober.Threading;

namespace uDerby
{
    public static class EntryPoint
    {
        public static void Main()
        {
            //TaskScheduler.UnobservedExceptionHandler += exception => Debug.Print(exception.ToString());

            // warmup task engine
            Task.Run(() => { })
                .Wait();

            //wow, an IoT Pinewood Derby car? add some sensors...
            var car = new Car();
            car.Start();
        }
    }
}