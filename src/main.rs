mod domain;
mod application;
mod infrastructure;

//use domain::user::User;
use domain::user_error::UserError;
use domain::email::EmailError;
use application::auth_service::AuthService;

fn main() {
    println!("Hello, world!");
    let user1 = AuthService::register(
        1,
        "test@mail.com".to_string(),
        "passHash".to_string(),);
    let user2 = AuthService::register(
        2,
        "test_mail-invalido.com".to_string(),
        "123".to_string(),);

    match user1 {
        Ok(u) => println!("Usuario creado: {:?}", u),
        Err(UserError::Email(EmailError::InvalidFormat)) => { println!("Error: e-mail no válido") }
        Err(UserError::EmptyPassword) => { println!("Error: password vacío") }
        Err(UserError::Email(EmailError::Empty)) => { println!("Error: email vacio"); }
    }

    match user2 {
        Ok(u) => println!("Usuario creado: {:?}", u),
        Err(UserError::Email(EmailError::InvalidFormat)) => { println!("Error: e-mail no válido") }
        Err(UserError::EmptyPassword) => { println!("Error: password vacío") }
        Err(UserError::Email(EmailError::Empty)) => { println!("Error: email vacio"); }
    }


}

