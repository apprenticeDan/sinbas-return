#[derive(Debug, Clone)]
pub struct User {
    pub id: u64,
    pub email: String,
    pub password_hash: String,
}

/*
impl User {
    pub fn new(id: u64, email: String, password_hash: String) -> Self {
        Self { id, email, password_hash, }
    }
}
*/

impl User {
    pub fn create(id: u64, email: String, password_hash: String) -> Result<Self, String> {
        if !email.contains("@") { return Err("Email inválido".to_string()); }
        if password_hash.is_empty() { return Err("Password vacío".to_string()); }

        Ok(Self { id, email, password_hash, })
    } 
}
