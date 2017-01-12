using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MySql.Data.MySqlClient;

namespace MYSQL_Database
{
    public class Database_Manager
    {
        public enum MySqlDataTypes
        { 
            NULL, INT, STRING, DATE, BIT, ENUM, DOUBLE, BLOB, FLOAT
        }

        protected string connectionString;            
        protected MySqlConnection sqlConn;                              //conection to database
        protected List<string> tableList;                               //list of the tables in currently connected database
        protected List<string>[] columnsOfTables;                       //list of the column names for each table
        protected Dictionary<string, MySqlDataTypes>[] dataTypesOfColumns;            //list of the data type of each column of each table
        protected Dictionary<string, int> tableIndexDictionary;         //dictionary to be used to find correct index for columnsOfTables and dataTypesOfColumns for a passed in tableName
        protected List<MySqlException> errors;                                  //list for holding all of the MySqlException errors
        private int argumentCounter;
        
        /// <summary>
        /// Creates the database manager and connects to the database
        /// </summary>
        /// <param name="database">the name of the database being connected to. i.e. 'alenia database'</param>
        /// <param name="server">the name of the server where the database is located. i.e. 'localhost'</param>
        /// <param name="userName">the username to be used to connect to the database</param>
        /// <param name="password">the password for the user being connected</param>
        protected Database_Manager(string database, string server, string userName, string password, int timeout = -1)
        {
            if (timeout > 0)
            {
                connectionString = "Database=" + database + ";Data Source=" + server + ";User Id=" + userName + ";Password=" + password + ";Connection Timeout=" + timeout;
            }
            else
            {
                connectionString = "Database=" + database + ";Data Source=" + server + ";User Id=" + userName + ";Password=" + password;            
            }
            try
            {
                sqlConn = new MySqlConnection(connectionString);            //create the connection
                sqlConn.Open();                                             //connect to the database
                tableList = new List<string>();
                tableIndexDictionary = new Dictionary<string, int>();
                errors = new List<MySqlException>();
                int index = 0;
                string query = "show tables";
            
                MySqlCommand listTables = new MySqlCommand(query, sqlConn);
                MySqlDataReader reader = listTables.ExecuteReader();
                while (reader.Read())
                {
                    tableList.Add(reader.GetString(0));                         //add the name of the table to the list
                    tableIndexDictionary.Add(reader.GetString(0), index++);     //store the index of this name in the list in the dictionary
                }
                reader.Close();
                columnsOfTables = new List<string>[tableList.Count];
                dataTypesOfColumns = new Dictionary<string, MySqlDataTypes>[tableList.Count];
                for (int i = 0; i < tableList.Count; i++)
                {                                           //go through each table 
                    columnsOfTables[i] = new List<string>();
                    dataTypesOfColumns[i] = new Dictionary<string, MySqlDataTypes>();
                    query = "show columns from " + tableList.ElementAt(i);
                    MySqlCommand listColumns = new MySqlCommand(query, sqlConn);
                    reader = listColumns.ExecuteReader();
                    while (reader.Read())
                    {
                        columnsOfTables[i].Add(reader.GetString(0));        //add each column's name to the list for this table

                        dataTypesOfColumns[i].Add(reader.GetString(0) ,convertStringToMySqlDataType(reader.GetString(1)));           //get the datatypes of each column
                    }
                    reader.Close();
                }
            }
            catch (MySqlException e)
            {
                e.Data.Add("Instruction Type", "Connect");
                errors.Add(e);
            }
        }

        ~Database_Manager()     //when destroying this object be sure to so close the MYSQL connection
        {
            sqlConn.Close();
        }

        /// <summary>
        /// Removes all error strings out of the error list
        /// </summary>
        protected void clearAllErrors()
        {
            errors.Clear();
        }

        /// <summary>
        /// Returns a string with all of the MySQL errors in it. Each error separated by a new line.
        /// </summary>
        /// <returns>A string with all of the MySQL errors separated by new lines.</returns>
        protected string returnAllErrorMessages()
        {
            string ret = "";
            for (int i = 0; i < errors.Count; i++)
            {
                ret += errors[i].Data["Instruction Type"] + ": " + errors[i].Message + "\r\n";
            }
            return ret;
        }

        public void resetArgumentCounter()
        {
            argumentCounter = 0;
        }

        /// <summary>
        /// converts a string value into an enum for MySql data types
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private static MySqlDataTypes convertStringToMySqlDataType(string value)
        {
            if (value.ToUpper().Contains("VARCHAR"))
            {
                return MySqlDataTypes.STRING;
            }
            else if (value.ToUpper().Contains("INT"))
            {
                return MySqlDataTypes.INT;
            }
            else if (value.ToUpper().Contains("DATE"))
            {
                return MySqlDataTypes.DATE;
            }
            else if (value.ToUpper().Contains("BIT"))
            {
                return MySqlDataTypes.BIT;
            }
            else if (value.ToUpper().Contains("ENUM"))
            {
                return MySqlDataTypes.ENUM;
            }
            else if (value.ToUpper().Contains("DOUBLE"))
            {
                return MySqlDataTypes.DOUBLE;
            }
            else if (value.ToUpper().Contains("BLOB"))
            {
                return MySqlDataTypes.BLOB;
            }
            else if (value.ToUpper().Contains("FLOAT"))
            {
                return MySqlDataTypes.FLOAT;
            }
            return MySqlDataTypes.NULL;
        }

        /// <summary>
        /// Adds a new row into one of the tables of this database
        /// </summary>
        /// <param name="tableName">the name of the table we want to add a row into</param>
        /// <param name="rowElements">the elements of the row being added in, each key value pair in the list is an element of the row
        /// where the key is a column name and the value is the element value at the column specified by the key value</param>
        /// <returns>true if row successfully added to table, false otherwise</returns>
        protected bool addRow(string tableName, List<KeyValuePair<string, string>> rowElements)
        {
            if (rowElements.Count > 0)
            {
                string columnSeq = "(";
                string argumentSeq = "(";
                for (int i = 0; i < rowElements.Count; i++)
                {
                    columnSeq += rowElements.ElementAt(i).Key;
                    argumentSeq += "@" + rowElements.ElementAt(i).Key;
                    if (i + 1 < rowElements.Count)
                    {
                        columnSeq += ", ";
                        argumentSeq += ", ";
                    }
                }
                columnSeq += ")";
                argumentSeq += ")";
                string query = "INSERT INTO " + tableName + columnSeq + " values" + argumentSeq;
                try
                {
                    MySqlCommand addRow = new MySqlCommand(query, sqlConn);
                    addRow.Prepare();
                    foreach (KeyValuePair<string, string> pair in rowElements)
                    {
                        if (dataTypesOfColumns[tableIndexDictionary[tableName]][pair.Key] == MySqlDataTypes.BIT)
                        {                                               //if the data type is a bit make sure we don't pass in a string
                            if (pair.Value.ToUpper().Equals("TRUE") || pair.Value.Equals("1"))
                            {
                                addRow.Parameters.AddWithValue("@" + pair.Key, 1);          //this protects agains SQL injection attacks
                            }
                            else
                            {
                                addRow.Parameters.AddWithValue("@" + pair.Key, 0);          //this protects agains SQL injection attacks                  
                            }
                        }
                        else
                        {
                            addRow.Parameters.AddWithValue("@" + pair.Key, pair.Value);     //this protects agains SQL injection attacks
                        }
                    }
                    addRow.ExecuteNonQuery();
                    return true;
                }
                catch (MySqlException e)
                {
                    e.Data.Add("Instruction Type", "Insert");
                    errors.Add(e);
                    return false;
                }
            }
            return false;
        }

        /// <summary>
        /// Adds a new row into one of the tables of this database
        /// </summary>
        /// <param name="tableName">the name of the table we want to add a row into</param>
        /// <param name="stringRowElements">the string elements of the row being added in, each key value pair in the list is an element of the row
        /// where the key is a column name and the value is the element value at the column specified by the key value</param>
        /// <param name="byteRowElements">the byte elements of the row being added in, each key value pair in the list is an element of the row
        /// where the key is a column name and the value is the element value at the column specified by the key value</param>
        /// <returns>true if row successfully added to table, false otherwise</returns>
        protected bool addRow(string tableName, List<KeyValuePair<string, string>> stringRowElements, List<KeyValuePair<string, byte[]>> byteRowElements)
        {
            if (stringRowElements.Count > 0 || byteRowElements.Count > 0)
            {
                string columnSeq = "(";
                string argumentSeq = "(";
                for (int i = 0; i < stringRowElements.Count; i++)
                {
                    columnSeq += stringRowElements.ElementAt(i).Key;
                    argumentSeq += "@" + stringRowElements.ElementAt(i).Key;
                    if (i + 1 < stringRowElements.Count || byteRowElements.Count > 0)
                    {
                        columnSeq += ", ";
                        argumentSeq += ", ";
                    }
                }
                for (int i = 0; i < byteRowElements.Count; i++)
                {
                    columnSeq += byteRowElements.ElementAt(i).Key;
                    argumentSeq += "@" + byteRowElements.ElementAt(i).Key;
                    if (i + 1 < byteRowElements.Count)
                    {
                        columnSeq += ", ";
                        argumentSeq += ", ";
                    }
                }
                columnSeq += ")";
                argumentSeq += ")";
                string query = "INSERT INTO " + tableName + columnSeq + " values" + argumentSeq;
                try
                {
                    MySqlCommand addRow = new MySqlCommand(query, sqlConn);
                    addRow.Prepare();
                    foreach (KeyValuePair<string, string> pair in stringRowElements)
                    {
                        if (dataTypesOfColumns[tableIndexDictionary[tableName]][pair.Key] == MySqlDataTypes.BIT)
                        {                                               //if the data type is a bit make sure we don't pass in a string
                            if (pair.Value.ToUpper().Equals("TRUE") || pair.Value.Equals("1"))
                            {
                                addRow.Parameters.AddWithValue("@" + pair.Key, 1);          //this protects agains SQL injection attacks
                            }
                            else
                            {
                                addRow.Parameters.AddWithValue("@" + pair.Key, 0);          //this protects agains SQL injection attacks                  
                            }
                        }
                        else
                        {
                            addRow.Parameters.AddWithValue("@" + pair.Key, pair.Value);     //this protects agains SQL injection attacks
                        }
                    }
                    foreach (KeyValuePair<string, byte[]> pair in byteRowElements)
                    {
                        addRow.Parameters.AddWithValue("@" + pair.Key, pair.Value);     //this protects agains SQL injection attacks
                    }
                    addRow.ExecuteNonQuery();
                    return true;
                }
                catch (MySqlException e)
                {
                    e.Data.Add("Instruction Type", "Insert");
                    errors.Add(e);
                    return false;
                }
            }
            return false;
        }

        /// <summary>
        /// Deletes all rows from a table where the row's values meet certain conditions
        /// </summary>
        /// <param name="tableName">the name of the table being deleted from</param>
        /// <param name="whereArguments">a list of delete values, each pair is like searching for all rows with [key] = [value],
        /// all of these key-value pairs are ANDed together</param>
        /// <returns>true if the command to delete from the table completed successfully, false otherwise</returns>
        protected bool deleteRow(string tableName, List<KeyValuePair<string, string>> whereArguments)
        {
            try
            {
                argumentCounter = 0;
                string query = "DELETE FROM " + tableName + " " + constructAndWhereString(tableName, whereArguments);
                MySqlCommand deleteRow = new MySqlCommand(query, sqlConn);
                deleteRow.Prepare();
                int localArgumentCounter = 0;
                if (whereArguments != null)
                {
                    foreach (KeyValuePair<string, string> pair in whereArguments)           //prevent SQL injection attacks
                    {
                        if (dataTypesOfColumns[tableIndexDictionary[tableName]][pair.Key] == MySqlDataTypes.BIT)
                        {                                               //if the data type is a bit make sure we don't pass in a string
                            if (pair.Value.ToUpper().Equals("TRUE") || pair.Value.Equals("1"))
                            {
                                deleteRow.Parameters.AddWithValue("@wArgument" + localArgumentCounter++, 1);          //this protects agains SQL injection attacks
                            }
                            else
                            {
                                deleteRow.Parameters.AddWithValue("@wArgument" + localArgumentCounter++, 0);          //this protects agains SQL injection attacks                  
                            }
                        }
                        else
                        {
                            deleteRow.Parameters.AddWithValue("@wArgument" + localArgumentCounter++, pair.Value);
                        }
                    }
                }
                deleteRow.ExecuteNonQuery();
                return true;
            }
            catch (MySqlException e)
            {
                e.Data.Add("Instruction Type", "Delete");
                errors.Add(e);
                return false;
            }
        }

        /// <summary>
        /// Deletes all rows from a table where the row's values meet certain conditions
        /// </summary>
        /// <param name="tableName">the name of the table being deleted from</param>
        /// <param name="whereArguments">a list of delete values, each pair is like searching for all rows with [key] > [value],
        /// all of these key-value pairs are ANDed together</param>
        /// <returns>true if the command to delete from the table completed successfully, false otherwise</returns>
        protected bool deleteRowsWhereValuesAreGreaterThan(string tableName, List<KeyValuePair<string, string>> whereArguments)
        {
            try
            {
                argumentCounter = 0;
                string query = "DELETE FROM " + tableName + " " + constructGreaterThanAndWhereString(tableName, whereArguments);
                MySqlCommand deleteRow = new MySqlCommand(query, sqlConn);
                deleteRow.Prepare();
                int localArgumentCounter = 0;
                if (whereArguments != null)
                {
                    foreach (KeyValuePair<string, string> pair in whereArguments)           //prevent SQL injection attacks
                    {
                        deleteRow.Parameters.AddWithValue("@wArgument" + localArgumentCounter++, pair.Value);
                    }
                }
                deleteRow.ExecuteNonQuery();
                return true;
            }
            catch (MySqlException e)
            {
                e.Data.Add("Instruction Type", "Delete");
                errors.Add(e);
                return false;
            }
        }

        /// <summary>
        /// builds the string for a where section of a command
        /// </summary>
        /// <param name="tableName">the name of the table this where string is being constructed for</param>
        /// <param name="whereArguments">a list of search values, each pair is like searching for all rows with [key] "LESSTHAN" [value],
        /// all of these key-value pairs and ANDed together</param>
        /// <returns>a string of less than statements, seperated by ANDs</returns>
        protected string constructGreaterThanAndWhereString(string tableName, List<KeyValuePair<string, string>> whereArguments)
        {
            string wherePart = "";
            if (whereArguments != null && whereArguments.Count > 0)
            {
                wherePart = " WHERE ";
                for (int i = 0; i < whereArguments.Count; i++)              //construct the "where" section of the command
                {
                    wherePart += whereArguments.ElementAt(i).Key + " > @wArgument" + argumentCounter++;
                    if (i + 1 < whereArguments.Count)
                    {
                        wherePart += " AND ";
                    }
                }
            }
            return wherePart;
        }

        /// <summary>
        /// builds the string for a where section of a command 
        /// </summary>
        /// <param name="tableName">the name of the table this where string is being constructed for</param>
        /// <param name="whereArguments">a list of search values, each pair is like searching for all rows with [key] = [value],
        /// all of these key-value pairs are ANDed together</param>
        /// <returns>a string of assignment statements, seperated by ANDs</returns>
        protected string constructAndWhereString(string tableName, List<KeyValuePair<string, string>> whereArguments)
        {
            string wherePart = "";
            if (whereArguments != null && whereArguments.Count > 0)
            {
                wherePart = " WHERE ";
                for (int i = 0; i < whereArguments.Count; i++)              //construct the "where" section of the command
                {
                    if (dataTypesOfColumns[tableIndexDictionary[tableName]][whereArguments.ElementAt(i).Key] == MySqlDataTypes.STRING)
                    {
                        wherePart += whereArguments.ElementAt(i).Key + " LIKE @wArgument" + argumentCounter++;
                    }
                    else
                    {
                        wherePart += whereArguments.ElementAt(i).Key + " = @wArgument" + argumentCounter++;
                    }
                    if (i + 1 < whereArguments.Count)
                    {
                        wherePart += " AND ";
                    }
                }
            }
            return wherePart;
        }

        /// <summary>
        /// builds the string for a where section of a command 
        /// </summary>
        /// <param name="tableName">the name of the table this where string is being constructed for</param>
        /// <param name="whereArguments">a list of search values, each pair is like searching for all rows with [key] = [value],
        /// all of these key-value pairs are ORed together</param>
        /// <returns>a string of assignment statements, seperated by ORs</returns>
        protected string constructOrWhereString(string tableName, List<KeyValuePair<string, string>> whereArguments)
        {
            string wherePart = "";
            if (whereArguments != null && whereArguments.Count > 0)
            {
                wherePart = " WHERE ";
                for (int i = 0; i < whereArguments.Count; i++)              //construct the "where" section of the command
                {
                    if (dataTypesOfColumns[tableIndexDictionary[tableName]][whereArguments.ElementAt(i).Key] == MySqlDataTypes.STRING)
                    {
                        wherePart += whereArguments.ElementAt(i).Key + " LIKE @wArgument" + argumentCounter++;
                    }
                    else
                    {
                        wherePart += whereArguments.ElementAt(i).Key + " = @wArgument" + argumentCounter++;
                    }
                    if (i + 1 < whereArguments.Count)
                    {
                        wherePart += " OR ";
                    }
                }
            }
            return wherePart;
        }

        /// <summary>
        /// builds the string for a where section in a command
        /// </summary>
        /// <param name="tableName">the name of the table this where string is being constructed for</param>
        /// <param name="whereArguments">a list of search values, each tuple in the list is used as Item1 = key, 
        /// Item2 = key's value, Item3 = true indicates AND will be used before this assingment pair,
        /// false indicates OR will be used before this assignment pair
        /// </param>
        /// <returns>a string of assignment statments, seperated by ANDs or ORs</returns>
        protected string constructWhereString(string tableName, List<Tuple<string, string, bool>> whereArguments)
        {
            string wherePart = "";
            if (whereArguments != null && whereArguments.Count > 0)
            {
                wherePart = " WHERE ";
                for (int i = 0; i < whereArguments.Count; i++)              //construct the "where" section of the command
                {
                    if (dataTypesOfColumns[tableIndexDictionary[tableName]][whereArguments.ElementAt(i).Item1] == MySqlDataTypes.STRING)
                    {
                        wherePart += whereArguments.ElementAt(i).Item1 + " LIKE @wArgument" + argumentCounter++;
                    }
                    else
                    {
                        wherePart += whereArguments.ElementAt(i).Item1 + " = @wArgument" + argumentCounter++;
                    }
                    if (i + 1 < whereArguments.Count)
                    {
                        if (whereArguments.ElementAt(i + 1).Item3)
                            wherePart += " AND ";
                        else
                            wherePart += " OR ";
                    }
                }
            }
            return wherePart;
        }

        /// <summary>
        /// updates a table in the database with new passed in values where certain recquirements are met
        /// </summary>
        /// <param name="tableName">the name of the table being updated</param>
        /// <param name="rowElements">the elements of the row being added in, each key value pair in the list is an element of the row
        /// where the key is a column name and the value is the element value at the column specified by the key value</param>
        /// <param name="whereArguments">a list of search values, each pair is like searching for all rows with [key] = [value],
        /// all of these key-value pairs are ANDed together</param>
        /// <returns>true if rows successfully updated in table, false otherwise</returns>
        protected bool update(string tableName, List<KeyValuePair<string, string>> rowElements, List<KeyValuePair<string, string>> whereArguments)
        {
            if (rowElements.Count > 0)
            {
                argumentCounter = 0;
                string query = "UPDATE " + tableName;
                string setPhrase = " SET ";
                for (int i = 0; i < rowElements.Count; i++)                     //construct the setting part of the phrase
                {
                    setPhrase += rowElements[i].Key + " = @uArgument" + argumentCounter++;      //Don't worry about a SQL injection into the key section, a user should never have access that part of the argument that is fully on the programmer
                    if (i + 1 < rowElements.Count)                                      //so unless the programmer is injecting malicious code into his own code this won't be a problem
                    {
                        setPhrase += ", ";
                    }
                }
                string wherePart = constructAndWhereString(tableName, whereArguments);
                query += setPhrase + wherePart;
                try
                {
                    int localArgumentCounter = 0;
                    MySqlCommand updateRows = new MySqlCommand(query, sqlConn);
                    updateRows.Prepare();
                    foreach (KeyValuePair<string, string> pair in rowElements)              //protect against SQL injection in the assignment in this statement
                    {
                        if (pair.Value.Equals(string.Empty))                    //if the empty string is passed on set phrase we want to set that cell to null
                        {
                            updateRows.Parameters.AddWithValue("@uArgument" + localArgumentCounter++, null);
                        }
                        else
                        {
                            if (dataTypesOfColumns[tableIndexDictionary[tableName]][pair.Key] == MySqlDataTypes.BIT)
                            {                                               //if the data type is a bit make sure we don't pass in a string
                                if (pair.Value.ToUpper().Equals("TRUE") || pair.Value.Equals("1"))
                                {
                                    updateRows.Parameters.AddWithValue("@uArgument" + localArgumentCounter++, 1);          //this protects agains SQL injection attacks
                                }
                                else
                                {
                                    updateRows.Parameters.AddWithValue("@uArgument" + localArgumentCounter++, 0);          //this protects agains SQL injection attacks                  
                                }
                            }
                            else
                            {
                                updateRows.Parameters.AddWithValue("@uArgument" + localArgumentCounter++, pair.Value);
                            }
                        }
                    }
                    if (whereArguments != null)
                    {
                        foreach (KeyValuePair<string, string> pair in whereArguments)       //protect against SQL injections in the where section if there is one
                        {
                            if (dataTypesOfColumns[tableIndexDictionary[tableName]][pair.Key] == MySqlDataTypes.BIT)
                            {                                               //if the data type is a bit make sure we don't pass in a string
                                if (pair.Value.ToUpper().Equals("TRUE") || pair.Value.Equals("1"))
                                {
                                    updateRows.Parameters.AddWithValue("@wArgument" + localArgumentCounter++, 1);          //this protects agains SQL injection attacks
                                }
                                else
                                {
                                    updateRows.Parameters.AddWithValue("@wArgument" + localArgumentCounter++, 0);          //this protects agains SQL injection attacks                  
                                }
                            }
                            else
                            {
                                updateRows.Parameters.AddWithValue("@wArgument" + localArgumentCounter++, pair.Value);
                            }
                        }
                    }
                    updateRows.ExecuteNonQuery();
                    return true;
                }
                catch (MySqlException e)
                {
                    e.Data.Add("Instruction Type", "Update");
                    errors.Add(e);
                    return false;
                }
            }
            return false;
        }

        /// <summary>
        /// updates a table in the database with new passed in values where certain recquirements are met
        /// </summary>
        /// <param name="tableName">the name of the table being updated</param>
        /// <param name="stringRowElements">the string elements of the row being added in, each key value pair in the list is an element of the row
        /// where the key is a column name and the value is the element value at the column specified by the key value</param>
        /// <param name="byteRowElements">the byte elements of the row being added in, each key value pair in the list is an element of the row
        /// where the key is a column name and the value is the element value at the column specified by the key value</param>
        /// <param name="whereArguments">a list of search values, each pair is like searching for all rows with [key] = [value],
        /// all of these key-value pairs are ANDed together</param>
        /// <returns>true if rows successfully updated in table, false otherwise</returns>
        protected bool update(string tableName, List<KeyValuePair<string, string>> stringRowElements, List<KeyValuePair<string, byte[]>> byteRowElements, List<KeyValuePair<string, string>> whereArguments)
        {
            if (stringRowElements.Count > 0 || byteRowElements.Count > 0)
            {
                argumentCounter = 0;
                string query = "UPDATE " + tableName;
                string setPhrase = " SET ";
                for (int i = 0; i < stringRowElements.Count; i++)                     //construct the setting part of the phrase
                {
                    setPhrase += stringRowElements[i].Key + " = @uArgument" + argumentCounter++;      //Don't worry about a SQL injection into the key section, a user should never have access that part of the argument that is fully on the programmer
                    if (i + 1 < stringRowElements.Count || byteRowElements.Count > 0)                                      //so unless the programmer is injecting malicious code into his own code this won't be a problem
                    {
                        setPhrase += ", ";
                    }
                }
                for (int i = 0; i < byteRowElements.Count; i++)
                {
                    setPhrase += byteRowElements[i].Key + " = @uArgument" + argumentCounter++;      //Don't worry about a SQL injection into the key section, a user should never have access that part of the argument that is fully on the programmer
                    if (i + 1 < byteRowElements.Count)                                      //so unless the programmer is injecting malicious code into his own code this won't be a problem
                    {
                        setPhrase += ", ";
                    }
                }
                string wherePart = constructAndWhereString(tableName, whereArguments);
                query += setPhrase + wherePart;
                try
                {
                    int localArgumentCounter = 0;
                    MySqlCommand updateRows = new MySqlCommand(query, sqlConn);
                    updateRows.Prepare();
                    foreach (KeyValuePair<string, string> pair in stringRowElements)              //protect against SQL injection in the assignment in this statement
                    {
                        if (pair.Value.Equals(string.Empty))                    //if the empty string is passed on set phrase we want to set that cell to null
                        {
                            updateRows.Parameters.AddWithValue("@uArgument" + localArgumentCounter++, null);
                        }
                        else
                        {
                            if (dataTypesOfColumns[tableIndexDictionary[tableName]][pair.Key] == MySqlDataTypes.BIT)
                            {                                               //if the data type is a bit make sure we don't pass in a string
                                if (pair.Value.ToUpper().Equals("TRUE") || pair.Value.Equals("1"))
                                {
                                    updateRows.Parameters.AddWithValue("@uArgument" + localArgumentCounter++, 1);          //this protects agains SQL injection attacks
                                }
                                else
                                {
                                    updateRows.Parameters.AddWithValue("@uArgument" + localArgumentCounter++, 0);          //this protects agains SQL injection attacks                  
                                }
                            }
                            else
                            {
                                updateRows.Parameters.AddWithValue("@uArgument" + localArgumentCounter++, pair.Value);
                            }
                        }
                    }
                    foreach (KeyValuePair<string, byte[]> pair in byteRowElements)              //protect against SQL injection in the assignment in this statement
                    {
                        if (pair.Value.Equals(string.Empty))                    //if the empty string is passed on set phrase we want to set that cell to null
                        {
                            updateRows.Parameters.AddWithValue("@uArgument" + localArgumentCounter++, null);
                        }
                        else
                        {
                            updateRows.Parameters.AddWithValue("@uArgument" + localArgumentCounter++, pair.Value);
                        }
                    }
                    if (whereArguments != null)
                    {
                        foreach (KeyValuePair<string, string> pair in whereArguments)       //protect against SQL injections in the where section if there is one
                        {
                            if (dataTypesOfColumns[tableIndexDictionary[tableName]][pair.Key] == MySqlDataTypes.BIT)
                            {                                               //if the data type is a bit make sure we don't pass in a string
                                if (pair.Value.ToUpper().Equals("TRUE") || pair.Value.Equals("1"))
                                {
                                    updateRows.Parameters.AddWithValue("@wArgument" + localArgumentCounter++, 1);          //this protects agains SQL injection attacks
                                }
                                else
                                {
                                    updateRows.Parameters.AddWithValue("@wArgument" + localArgumentCounter++, 0);          //this protects agains SQL injection attacks                  
                                }
                            }
                            else
                            {
                                updateRows.Parameters.AddWithValue("@wArgument" + localArgumentCounter++, pair.Value);
                            }
                        }
                    }
                    updateRows.ExecuteNonQuery();
                    return true;
                }
                catch (MySqlException e)
                {
                    e.Data.Add("Instruction Type", "Update");
                    errors.Add(e);
                    return false;
                }
            }
            return false;
        }

        /// <summary>
        /// Searches a table in the database for the specified values
        /// </summary>
        /// <param name="tableName">the name of the table being searched</param>
        /// <param name="whereArguments">a list of search values, each pair is like searching for all rows with [key] = [value],
        /// all of these key-value pairs are ANDed together</param>
        /// <param name="columnsToReturn">a string each containing the name of a column you want to return the values of, '*' returns the values of every column</param>
        /// <returns>A dictionary for each row that matched, the dictionary will contain the values for each column associated with the column name</returns>
        protected List<Dictionary<string, string>> search(string tableName, List<KeyValuePair<string, string>> whereArguments, params string[] columnsToReturn)
        {
            argumentCounter = 0;
            List<Dictionary<string, string>> ret = new List<Dictionary<string, string>>();
            string selectPart = "SELECT ";
            int numberOFColumnValuesReturning = columnsToReturn.Length;
            string[] allColumns;                //array for holding all of the names of the columns that will have values returned for
            if (columnsToReturn[0] == "*")
            {
                selectPart += "*";
                numberOFColumnValuesReturning = columnsOfTables[tableIndexDictionary[tableName]].Count;
                allColumns = new string[numberOFColumnValuesReturning];
                for (int i = 0; i < numberOFColumnValuesReturning; i++)             //star was passed in so mush look up all of the columns for this table
                {
                    allColumns[i] = columnsOfTables[tableIndexDictionary[tableName]].ElementAt(i);
                }
            }
            else
            {
                allColumns = new string[columnsToReturn.Length];
                for (int i = 0; i < columnsToReturn.Length; i++)
                {
                    selectPart += columnsToReturn[i];
                    allColumns[i] = columnsToReturn[i];                     //a set of columns was passed in, just copy them into the array
                    if (i + 1 < columnsToReturn.Length)
                    {
                        selectPart += ", ";
                    }
                }
            }
            string wherePart = constructAndWhereString(tableName, whereArguments);

            try
            {
                int localArgumentCounter = 0;
                string query = selectPart + " FROM " + tableName + wherePart;
                MySqlCommand selectCommand = new MySqlCommand(query, sqlConn);
                selectCommand.Prepare();
                if (whereArguments != null)
                {
                    foreach (KeyValuePair<string, string> pair in whereArguments)
                    {
                        if (dataTypesOfColumns[tableIndexDictionary[tableName]][pair.Key] == MySqlDataTypes.BIT)
                        {                                               //if the data type is a bit make sure we don't pass in a string
                            if (pair.Value.ToUpper().Equals("TRUE") || pair.Value.Equals("1"))
                            {
                                selectCommand.Parameters.AddWithValue("@wArgument" + localArgumentCounter++, 1);          //this protects agains SQL injection attacks
                            }
                            else
                            {
                                selectCommand.Parameters.AddWithValue("@wArgument" + localArgumentCounter++, 0);          //this protects agains SQL injection attacks                  
                            }
                        }
                        else
                        {
                            selectCommand.Parameters.AddWithValue("@wArgument" + localArgumentCounter++, pair.Value);
                        }
                    }
                }
                MySqlDataReader reader = selectCommand.ExecuteReader();

                while (reader.Read())
                {
                    Dictionary<string, string> thisReturnRow = new Dictionary<string, string>();    //each matching row will have its own dictionary
                    for (int i = 0; i < numberOFColumnValuesReturning; i++)
                    {
                        thisReturnRow.Add(allColumns[i], GetDBString(allColumns[i], reader));       //add in the column name and value into the dictionary
                    }
                    ret.Add(thisReturnRow);
                }
                reader.Close();
                return ret;
            }
            catch (MySqlException e)
            {
                e.Data.Add("Instruction Type", "Select");
                errors.Add(e);
            }
            return null;
        }

        /// <summary>
        /// Searches a table in the database for the specified values
        /// </summary>
        /// <param name="tableName">the name of the table being searched</param>
        /// <param name="whereArguments">a list of search values, each pair is like searching for all rows with [key] = [value],
        /// for each key-value pair the boolean in the tuple determines whether the pair is ANDed or ORed, true makes it ANDed</param>
        /// <param name="columnsToReturn">a string each containing the name of a column you want to return the values of, '*' returns the values of every column</param>
        /// <returns>A dictionary for each row that matched, the dictionary will contain the values for each column associated with the column name</returns>
        protected List<Dictionary<string, string>> search(string tableName, List<Tuple<string, string, bool>> whereArguments, params string[] columnsToReturn)
        {
            argumentCounter = 0;
            List<Dictionary<string, string>> ret = new List<Dictionary<string, string>>();
            string selectPart = "SELECT ";
            int numberOFColumnValuesReturning = columnsToReturn.Length;
            string[] allColumns;                //array for holding all of the names of the columns that will have values returned for
            if (columnsToReturn[0] == "*")
            {
                selectPart += "*";
                numberOFColumnValuesReturning = columnsOfTables[tableIndexDictionary[tableName]].Count;
                allColumns = new string[numberOFColumnValuesReturning];
                for (int i = 0; i < numberOFColumnValuesReturning; i++)             //star was passed in so mush look up all of the columns for this table
                {
                    allColumns[i] = columnsOfTables[tableIndexDictionary[tableName]].ElementAt(i);
                }
            }
            else
            {
                allColumns = new string[columnsToReturn.Length];
                for (int i = 0; i < columnsToReturn.Length; i++)
                {
                    selectPart += columnsToReturn[i];
                    allColumns[i] = columnsToReturn[i];                     //a set of columns was passed in, just copy them into the array
                    if (i + 1 < columnsToReturn.Length)
                    {
                        selectPart += ", ";
                    }
                }
            }
            string wherePart = constructWhereString(tableName, whereArguments);

            try
            {
                int localArgumentCounter = 0;
                string query = selectPart + " FROM " + tableName + wherePart;
                MySqlCommand selectCommand = new MySqlCommand(query, sqlConn);
                selectCommand.Prepare();
                if (whereArguments != null)
                {
                    foreach (Tuple<string, string, bool> pair in whereArguments)
                    {
                        if (dataTypesOfColumns[tableIndexDictionary[tableName]][pair.Item1] == MySqlDataTypes.BIT)
                        {                                               //if the data type is a bit make sure we don't pass in a string
                            if (pair.Item2.ToUpper().Equals("TRUE") || pair.Item2.Equals("1"))
                            {
                                selectCommand.Parameters.AddWithValue("@wArgument" + localArgumentCounter++, 1);          //this protects agains SQL injection attacks
                            }
                            else
                            {
                                selectCommand.Parameters.AddWithValue("@wArgument" + localArgumentCounter++, 0);          //this protects agains SQL injection attacks                  
                            }
                        }
                        else
                        {
                            selectCommand.Parameters.AddWithValue("@wArgument" + localArgumentCounter++, pair.Item2);
                        }
                    }
                }
                MySqlDataReader reader = selectCommand.ExecuteReader();

                while (reader.Read())
                {
                    Dictionary<string, string> thisReturnRow = new Dictionary<string, string>();    //each matching row will have its own dictionary
                    for (int i = 0; i < numberOFColumnValuesReturning; i++)
                    {
                        thisReturnRow.Add(allColumns[i], GetDBString(allColumns[i], reader));       //add in the column name and value into the dictionary
                    }
                    ret.Add(thisReturnRow);
                }
                reader.Close();
                return ret;
            }
            catch (MySqlException e)
            {
                e.Data.Add("Instruction Type", "Select");
                errors.Add(e);
            }
            return null;
        }

        /// <summary>
        /// Searches a table in the database for the specified values
        /// </summary>
        /// <param name="tableName">the name of the table being searched</param>
        /// <param name="whereArguments">a list of search values, each pair is like searching for all rows with [key] = [value],
        /// all of these key-value pairs are ANDed together</param>
        /// <param name="willSort">this data will be sorted</param>
        /// <param name="sortColumn">the column to sort the results by</param>
        /// <param name="orderAscending">the order to sort the results by, it's either ascending or descending</param>
        /// <param name="limit">how many results to limit this search to</param>
        /// <returns>A dictionary for each row that matched, the dictionary will contain the values for each column associated with the column name</returns>
        protected List<Dictionary<string, string>> search(string tableName, List<KeyValuePair<string, string>> whereArguments, bool willSort, string sortColumn = null, bool orderAscending = true, int limit = -1)
        {
            argumentCounter = 0;
            List<Dictionary<string, string>> ret = new List<Dictionary<string, string>>();
            string selectPart = "SELECT *";
            string wherePart = constructAndWhereString(tableName, whereArguments);
            try
            {
                string query = selectPart + " FROM " + tableName + wherePart;
                if (willSort)             //if this parameter was passed in, will sort columns by a column's values 
                {
                    query += " ORDER BY " + sortColumn;
                    if (orderAscending)
                    {
                        query += " ASC";
                    }
                    else
                    {
                        query += " DESC";
                    }
                }
                if (limit != -1)                        //if this parameter is passed in, will limit the number of results returned
                {
                    query += " LIMIT " + limit;
                }
                int localArgumentCounter = 0;
                MySqlCommand selectCommand = new MySqlCommand(query, sqlConn);
                selectCommand.Prepare();
                if (whereArguments != null)
                {
                    foreach (KeyValuePair<string, string> pair in whereArguments)
                    {
                        if (dataTypesOfColumns[tableIndexDictionary[tableName]][pair.Key] == MySqlDataTypes.BIT)
                        {                                               //if the data type is a bit make sure we don't pass in a string
                            if (pair.Value.ToUpper().Equals("TRUE") || pair.Value.Equals("1"))
                            {
                                selectCommand.Parameters.AddWithValue("@wArgument" + localArgumentCounter++, 1);          //this protects agains SQL injection attacks
                            }
                            else
                            {
                                selectCommand.Parameters.AddWithValue("@wArgument" + localArgumentCounter++, 0);          //this protects agains SQL injection attacks                  
                            }
                        }
                        else
                        {
                            selectCommand.Parameters.AddWithValue("@wArgument" + localArgumentCounter++, pair.Value);
                        }
                    }
                }
                MySqlDataReader reader = selectCommand.ExecuteReader();

                while (reader.Read())
                {
                    Dictionary<string, string> thisReturnRow = new Dictionary<string, string>();    //each matching row will have its own dictionary
                    for (int i = 0; i < columnsOfTables[tableIndexDictionary[tableName]].Count; i++)
                    {
                        thisReturnRow.Add(columnsOfTables[tableIndexDictionary[tableName]][i], GetDBString(columnsOfTables[tableIndexDictionary[tableName]][i], reader));       //add in the column name and value into the dictionary
                    }
                    ret.Add(thisReturnRow);
                }
                reader.Close();
                return ret;
            }
            catch (MySqlException e)
            {
                e.Data.Add("Instruction Type", "Select");
                errors.Add(e);
            }
            return null;
        }

        /// <summary>
        /// Searches a table in the database for the specified values
        /// </summary>
        /// <param name="tableName">the name of the table being searched</param>
        /// <param name="whereArguments">a list of search values, each pair is like searching for all rows with [key] = [value],
        /// all of these key-value pairs are ANDed together</param>
        /// <returns>A dictionary for each row that matched, the dictionary will contain the values for each column associated with the column name</returns>
        protected List<Dictionary<string, byte[]>> searchByte(string tableName, List<KeyValuePair<string, string>> whereArguments, params string[] columnsToReturn)
        {
            argumentCounter = 0;
            string selectPart = "SELECT ";
            int numberOFColumnValuesReturning = columnsToReturn.Length;
            string[] allColumns;                //array for holding all of the names of the columns that will have values returned for
            if (columnsToReturn[0] == "*")
            {
                selectPart += "*";
                numberOFColumnValuesReturning = columnsOfTables[tableIndexDictionary[tableName]].Count;
                allColumns = new string[numberOFColumnValuesReturning];
                for (int i = 0; i < numberOFColumnValuesReturning; i++)             //star was passed in so mush look up all of the columns for this table
                {
                    allColumns[i] = columnsOfTables[tableIndexDictionary[tableName]].ElementAt(i);
                }
            }
            else
            {
                allColumns = new string[columnsToReturn.Length];
                for (int i = 0; i < columnsToReturn.Length; i++)
                {
                    selectPart += columnsToReturn[i];
                    allColumns[i] = columnsToReturn[i];                     //a set of columns was passed in, just copy them into the array
                    if (i + 1 < columnsToReturn.Length)
                    {
                        selectPart += ", ";
                    }
                }
            }
            string wherePart = constructAndWhereString(tableName, whereArguments);
            try
            {
                int localArgumentCounter = 0;
                string query = selectPart + " FROM " + tableName + wherePart;
                MySqlCommand selectCommand = new MySqlCommand(query, sqlConn);
                selectCommand.Prepare();
                if (whereArguments != null)
                {
                    foreach (KeyValuePair<string, string> pair in whereArguments)
                    {
                        if (dataTypesOfColumns[tableIndexDictionary[tableName]][pair.Key] == MySqlDataTypes.BIT)
                        {                                               //if the data type is a bit make sure we don't pass in a string
                            if (pair.Value.ToUpper().Equals("TRUE") || pair.Value.Equals("1"))
                            {
                                selectCommand.Parameters.AddWithValue("@wArgument" + localArgumentCounter++, 1);          //this protects agains SQL injection attacks
                            }
                            else
                            {
                                selectCommand.Parameters.AddWithValue("@wArgument" + localArgumentCounter++, 0);          //this protects agains SQL injection attacks                  
                            }
                        }
                        else
                        {
                            selectCommand.Parameters.AddWithValue("@wArgument" + localArgumentCounter++, pair.Value);
                        }
                    }
                }
                List<Dictionary<string, byte[]>> ret = new List<Dictionary<string, byte[]>>();
                MySqlDataReader reader = selectCommand.ExecuteReader();
                while (reader.Read())
                {
                    Dictionary<string, byte[]> thisReturnRow = new Dictionary<string, byte[]>();    //each matching row will have its own dictionary
                    for (int i = 0; i < numberOFColumnValuesReturning; i++)
                    {
                        try
                        {
                            MySqlDataTypes thisColumnDataType = dataTypesOfColumns[tableIndexDictionary[tableName]][allColumns[i]];
                            switch (thisColumnDataType)         //add in the column name and value into the dictionary base on what type of data it is
                            {
                                case MySqlDataTypes.BLOB:
                                    thisReturnRow.Add(allColumns[i], (byte[])reader[allColumns[i]]);
                                    break;
                                case MySqlDataTypes.ENUM:
                                case MySqlDataTypes.DATE:
                                case MySqlDataTypes.STRING:
                                    string tempString = reader[allColumns[i]].ToString();
                                    List<byte> tempList = new List<byte>();
                                    char[] tempChar = tempString.ToCharArray();
                                    for (int m=0;m<tempChar.Length;m++)
                                    {
                                        byte[] oneCharacter = BitConverter.GetBytes(tempChar[m]);
                                        for (int l=0;l<oneCharacter.Length;l++)
                                        {
                                            tempList.Add(oneCharacter[l]);
                                        }
                                    }
                                    thisReturnRow.Add(allColumns[i], tempList.ToArray());
                                    break;
                                case MySqlDataTypes.DOUBLE:
                                    double tempDoulbe = (double)reader[allColumns[i]];
                                    thisReturnRow.Add(allColumns[i], BitConverter.GetBytes(tempDoulbe));
                                    break;
                                case MySqlDataTypes.FLOAT:
                                    float tempFloat = (float)reader[allColumns[i]];
                                    thisReturnRow.Add(allColumns[i], BitConverter.GetBytes(tempFloat));
                                    break;
                                case MySqlDataTypes.INT:
                                    int tempInt = (int)reader[allColumns[i]];
                                    thisReturnRow.Add(allColumns[i], BitConverter.GetBytes(tempInt));
                                    break;
                                case MySqlDataTypes.BIT:
                                    bool tempBool = (bool)reader[allColumns[i]];
                                    thisReturnRow.Add(allColumns[i], BitConverter.GetBytes(tempBool));
                                    break;
                            }
                                   
                        }
                        catch (InvalidCastException)
                        {
                            
                        }
                    }
                    ret.Add(thisReturnRow);
                }
                reader.Close();
                return ret;
            }
            catch (MySqlException e)
            {
                e.Data.Add("Instruction Type", "Select");
                errors.Add(e);
            }
            return null;
        }

        /// <summary>
        /// returns the string of whatever field you want
        /// </summary>
        /// <param name="SqlFieldName">the name of the column you want the value for</param>
        /// <param name="Reader">the reader that is already open and reading the values of a select statement</param>
        /// <returns>the string value of the requested field</returns>
        private static string GetDBString(string SqlFieldName, MySqlDataReader Reader)
        {
            if (Reader[SqlFieldName].Equals(DBNull.Value))          //on null values return ""
            {
                return String.Empty;
            }
            else
            {
                return Reader.GetString(SqlFieldName);
            }
            
        } 

    }
}
