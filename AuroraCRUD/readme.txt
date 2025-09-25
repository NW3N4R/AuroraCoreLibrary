on app.main.cs or program.cs give ConnectionService a sqlConnection
    1- first on start up
        get the models name space
        send it to CoreReflections.GetAllTableNames(); -- returns a list of string
    2- for each one of the model get its observable 

START LISTENING ON CHANGES
    1 - start a loop for each one of the observables 
        send the connection, the observable and the name space

         private async Task LoadAll()
        {
            var tables = CoreReflections.GetAllTableNames("AuroraMarketDesktop.Models");
            object? listener;
            foreach (var table in tables)
            {
                var observs = CoreReflections.GetObservableCollectionType(TempStore.Instance, table);
                if (observs == null)
                {
                    Debug.WriteLine($"couldn't start listener for {table} since we cant find its observable");
                    continue;
                }
                var innerType = observs.GetType().GetGenericArguments()[0];
                var genericType = typeof(DataBaseListener<>).MakeGenericType(innerType);
                listener = Activator.CreateInstance(genericType);
                dynamic dynListener = listener;
                dynListener.StartListening(); // if you have a method like this

            }
            await ConnectionService.OpenDatabaseConnection();
        }

LOADING ALL THE DATA

    1- loop through the table name list 
        find each observable collection by the table name which has been specified by the obsvr attr
        you get the obsrvs via CoreReflections.GetObservableCollectionType(x,y)
        -x is the tempstore instance,y is the table name

    then send this observable collection to StarterLoadup.LoadDataAsync<T> where T is the obsrv 
    this way we have loaded all the data

