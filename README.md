install it as a service (with admin privilege)
    tcpmon -install

uninstall it from service
    tcpmon -uninstall

run it normally as an command application
    tcpmon -normal
    
test email function
    tcpmon -testmail
    
Please rename settings-sample.json to settings.json and modify its content.

    
settings template (settings.json)

{
    "service":{
        "restart": true,
        "name": "dualshield",
        "condition":200,
        "timeout":120
    },
    "smtp": {
        "enabled": true,
        "server":"you email server fqdn or IP",
        "port":587,
        "ssl":true,
        "auth":true,
        "username":"email sender account",
        "password":"email sender password",
        "to":["email recipients"],
        "customize":{
            "subject":"Alert: There are a lot of CLOSE_WAIT connections",        
            "body":"Please check the log at {0} for details, and notify the vendor"
        }
        
    },
    "period": 600,
    "port":8074,
    "threshold":20,
    "state": 8

}

Note:

service:
    condition: when the total of CLOSE_WAIT sockets reaches this amount, the specified service is restarted
    timeout: we give 2 minutes (120 sec) for stopping and starting service

state: socket state. here we are interested in CLOSE_WAIT = 8
threshold: if the total exceeds it, notify admin by email
port: the port is being monitored
period: seconds, the monitor will check the socket status per 10 minutes if it is set to 600.


    MIB_TCP_STATE_CLOSED = 1,
    MIB_TCP_STATE_LISTEN = 2,
    MIB_TCP_STATE_SYN_SENT = 3,
    MIB_TCP_STATE_SYN_RCVD = 4,
    MIB_TCP_STATE_ESTAB = 5,
    MIB_TCP_STATE_FIN_WAIT1 = 6,
    MIB_TCP_STATE_FIN_WAIT2 = 7,
    MIB_TCP_STATE_CLOSE_WAIT = 8,
    MIB_TCP_STATE_CLOSING = 9,
    MIB_TCP_STATE_LAST_ACK = 10,
    MIB_TCP_STATE_TIME_WAIT = 11,
    MIB_TCP_STATE_DELETE_TCB = 12