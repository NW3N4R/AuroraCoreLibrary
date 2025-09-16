LOADING ALL THE DATA
1- first on start up
    get the models name space
    send it to CoreReflections.GetAllTableNames(); -- returns a list of string

2- loop through the table name list 
    find each observable collection by the table name which has been specified by the obsvr attr
    you get the obsrvs via CoreReflections.GetObservableCollectionType(x,y)
        -x is the tempstore instance,y is the table name

    then send this observable collection to StarterLoadup.LoadDataAsync<T> where T is the obsrv 
    this way we have loaded all the data

START LISTENING ON CHANGES