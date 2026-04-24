mod domain;
use domain::user::User;
use domain::user_error::UserError;

fn main() {
    println!("Hello, world!");
    let user = User::create(
        1,
        "test_mail-invalido.com".to_string(),
        "1234".to_string(),);

    match user {
        Ok(u) => println!("Usuario creado: {:?}", u),
        Err(UserError::InvalidEmail) => { println!("Error: e-mail no válido") }
        Err(UserError::EmptyPassword) => { println!("Error: password vacío") }
    }
}

