mod domain;
use domain::user::User;

fn main() {
    println!("Hello, world!");
    let user = User::create(1,"test@email.com".to_string(),"hashed_pass".to_string(),);

    match user {
        Ok(u) => println!("Usuario creado: {:?}", u),
        Err(e) => println!("Error: {}", e),
    }
}

